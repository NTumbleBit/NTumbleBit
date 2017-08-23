using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NTumbleBit.Services.RPC
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
		public Transaction Transaction
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
			if(_RefreshedAtBlock != currentBlock)
			{
				var newBlockCount = _RPCClient.GetBlockCount();
				//If we just udpated the value...
				if(Interlocked.Exchange(ref _BlockCount, newBlockCount) != newBlockCount)
				{
					_RefreshedAtBlock = currentBlock;
					ListTransactions();
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
					_BlockCount = _RPCClient.GetBlockCount();
				}
				return _BlockCount;
			}
		}

		public Transaction GetTransaction(uint256 txId)
		{
			RPCWalletEntry entry = null;
			if(_WalletEntries.TryGetValue(txId, out entry))
			{
				return entry.Transaction;
			}
			return null;
		}


		private static async Task<Transaction> FetchTransactionAsync(RPCClient rpc, uint256 txId)
		{
			try
			{

				var result = await rpc.SendCommandNoThrowsAsync("gettransaction", txId.ToString(), true).ConfigureAwait(false);
				var tx = new Transaction((string)result.Result["hex"]);
				return tx;
			}
			catch(RPCException ex)
			{
				if(ex.RPCCode == RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY)
					return null;
				throw;
			}
		}

		public ICollection<RPCWalletEntry> GetEntries()
		{
			return _WalletEntries.Values;
		}

		public IEnumerable<RPCWalletEntry> GetEntriesFromScript(Script script)
		{
			lock(_TxByScriptId)
			{
				IReadOnlyCollection<RPCWalletEntry> transactions = null;
				if(_TxByScriptId.TryGetValue(script, out transactions))
					return transactions.ToArray();
				return new RPCWalletEntry[0];
			}
		}

		private void AddTxByScriptId(uint256 txId, RPCWalletEntry entry)
		{
			IEnumerable<Script> scripts = GetScriptsOf(entry.Transaction);
			lock(_TxByScriptId)
			{
				foreach(var s in scripts)
				{
					_TxByScriptId.Add(s, entry);
				}
			}
		}

		private void RemoveTxByScriptId(RPCWalletEntry entry)
		{
			IEnumerable<Script> scripts = GetScriptsOf(entry.Transaction);
			lock(_TxByScriptId)
			{
				foreach(var s in scripts)
				{
					_TxByScriptId.Remove(s, entry);
				}
			}
		}

		private static IEnumerable<Script> GetScriptsOf(Transaction tx)
		{
			return tx.Outputs.Select(o => o.ScriptPubKey)
									.Concat(tx.Inputs.Select(o => o.GetSigner()?.ScriptPubKey))
									.Where(script => script != null);
		}

		MultiValueDictionary<Script, RPCWalletEntry> _TxByScriptId = new MultiValueDictionary<Script, RPCWalletEntry>();
		ConcurrentDictionary<uint256, RPCWalletEntry> _WalletEntries = new ConcurrentDictionary<uint256, RPCWalletEntry>();
		void ListTransactions()
		{
			var removeFromWalletEntries = new HashSet<uint256>(_WalletEntries.Keys);

			int count = 100;
			int skip = 0;
			int highestConfirmation = 0;

			while(true)
			{
				var result = _RPCClient.SendCommand("listtransactions", "*", count, skip, true);
				skip += count;
				var transactions = (JArray)result.Result;

				var batch = _RPCClient.PrepareBatch();
				var fetchingTransactions = new List<Tuple<RPCWalletEntry, Task<Transaction>>>();
				foreach(var obj in transactions)
				{
					var entry = new RPCWalletEntry();
					entry.Confirmations = obj["confirmations"] == null ? 0 : (int)obj["confirmations"];
					entry.TransactionId = new uint256((string)obj["txid"]);
					removeFromWalletEntries.Remove(entry.TransactionId);

					RPCWalletEntry existing;
					if(_WalletEntries.TryGetValue(entry.TransactionId, out existing))
					{
						existing.Confirmations = entry.Confirmations;
						entry = existing;
					}

					if(entry.Transaction == null)
					{
						fetchingTransactions.Add(Tuple.Create(entry, FetchTransactionAsync(batch, entry.TransactionId)));
					}
					if(obj["confirmations"] != null)
					{
						highestConfirmation = Math.Max(highestConfirmation, (int)obj["confirmations"]);
					}
				}
				batch.SendBatchAsync();

				foreach(var fetching in fetchingTransactions)
				{
					var entry = fetching.Item1;
					entry.Transaction = fetching.Item2.GetAwaiter().GetResult();
					if(entry.Transaction != null)
					{
						if(_WalletEntries.TryAdd(entry.TransactionId, entry))
							AddTxByScriptId(entry.TransactionId, entry);
					}
				}

				if(transactions.Count < count || highestConfirmation >= 1400)
					break;
			}
			foreach(var remove in removeFromWalletEntries)
			{
				RPCWalletEntry opt;
				_WalletEntries.TryRemove(remove, out opt);
			}
		}

		public void ImportTransaction(Transaction transaction, int confirmations)
		{
			var txId = transaction.GetHash();
			var entry = new RPCWalletEntry()
			{
				Confirmations = confirmations,
				TransactionId = transaction.GetHash(),
				Transaction = transaction
			};
			if(_WalletEntries.TryAdd(txId, entry))
				AddTxByScriptId(txId, entry);
		}
	}
}
