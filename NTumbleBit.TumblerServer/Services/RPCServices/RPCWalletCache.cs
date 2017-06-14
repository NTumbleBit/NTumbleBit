using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#if !CLIENT
namespace NTumbleBit.TumblerServer.Services.RPCServices
#else
namespace NTumbleBit.Client.Tumbler.Services.RPCServices
#endif
{
	public class RPCWalletEntry
	{
		public uint256 TransactionId
		{
			get; set;
		}
		public int Confirmations
		{
			get; set;
		}
	}

	/// <summary>
	/// Workaround around slow Bitcoin Core RPC. 
	/// We are refreshing the list of received transaction once per block.
	/// </summary>
	public class RPCWalletCache
	{
		private readonly RPCClient _RPCClient;
		private readonly IRepository _Repo;
		public RPCWalletCache(RPCClient rpc, IRepository repository)
		{
			if(rpc == null)
				throw new ArgumentNullException("rpc");
			if(repository == null)
				throw new ArgumentNullException("repository");
			_RPCClient = rpc;
			_Repo = repository;
		}

		volatile uint256 _RefreshedAtBlock;

		public void Refresh(uint256 currentBlock)
		{
			var refreshedAt = _RefreshedAtBlock;
			if(refreshedAt != currentBlock)
			{
				lock(_Transactions)
				{
					if(refreshedAt != currentBlock)
					{
						RefreshBlockCount();
						_Transactions = ListTransactions(ref _KnownTransactions);
						_RefreshedAtBlock = currentBlock;
					}
				}
			}
		}

		int _BlockCount;
		public int BlockCount
		{
			get
			{
				if(_BlockCount == 0)
				{
					RefreshBlockCount();
				}
				return _BlockCount;
			}
		}

		private void RefreshBlockCount()
		{
			Interlocked.Exchange(ref _BlockCount, _RPCClient.GetBlockCount());
		}

		public Transaction GetTransaction(uint256 txId)
		{
			var cached = GetCachedTransaction(txId);
			if(cached != null)
				return cached;
			var tx = FetchTransaction(txId);
			if(tx == null)
				return null;
			PutCached(tx);
			return tx;
		}


		private Transaction FetchTransaction(uint256 txId)
		{
			try
			{
				//check in the wallet tx
				var result = _RPCClient.SendCommandNoThrows("gettransaction", txId.ToString(), true);
				if(result == null || result.Error != null)
				{
					//check in the txindex
					result = _RPCClient.SendCommandNoThrows("getrawtransaction", txId.ToString(), 1);
					if(result == null || result.Error != null)
						return null;
				}
				var tx = new Transaction((string)result.Result["hex"]);
				return tx;
			}
			catch(RPCException) { return null; }
		}

		public RPCWalletEntry[] GetEntries()
		{
			lock(_Transactions)
			{
				return _Transactions.ToArray();
			}
		}

		private void PutCached(Transaction tx)
		{
			_Repo.UpdateOrInsert("CachedTransactions", tx.GetHash().ToString(), tx, (a, b) => b);
		}

		private Transaction GetCachedTransaction(uint256 txId)
		{
			return _Repo.Get<Transaction>("CachedTransactions", txId.ToString());
		}


		List<RPCWalletEntry> _Transactions = new List<RPCWalletEntry>();
		HashSet<uint256> _KnownTransactions = new HashSet<uint256>();
		List<RPCWalletEntry> ListTransactions(ref HashSet<uint256> knownTransactions)
		{
			List<RPCWalletEntry> array = new List<RPCWalletEntry>();
			knownTransactions = new HashSet<uint256>();
			int count = 100;
			int skip = 0;
			int highestConfirmation = 0;

			while(true)
			{
				var result = _RPCClient.SendCommandNoThrows("listtransactions", "*", count, skip, true);
				skip += count;
				if(result.Error != null)
					return null;
				var transactions = (JArray)result.Result;
				foreach(var obj in transactions)
				{
					var entry = new RPCWalletEntry();
					entry.Confirmations = obj["confirmations"] == null ? 0 : (int)obj["confirmations"];
					entry.TransactionId = new uint256((string)obj["txid"]);
					if(knownTransactions.Add(entry.TransactionId))
						array.Add(entry);
					if(obj["confirmations"] != null)
					{
						highestConfirmation = Math.Max(highestConfirmation, (int)obj["confirmations"]);
					}
				}
				if(transactions.Count < count || highestConfirmation >= 1400)
					break;
			}
			return array;
		}


		public void ImportTransaction(Transaction transaction, int confirmations)
		{
			PutCached(transaction);
			lock(_Transactions)
			{
				if(_KnownTransactions.Add(transaction.GetHash()))
				{
					_Transactions.Insert(0,
						new RPCWalletEntry()
						{
							Confirmations = confirmations,
							TransactionId = transaction.GetHash()
						});
				}
			}
		}
	}
}
