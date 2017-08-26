using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using NTumbleBit.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
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
					var startTime = DateTimeOffset.UtcNow;
					ListTransactions();
					Logs.Wallet.LogInformation($"Updated {_WalletEntries.Count} cached transactions in {(long)(DateTimeOffset.UtcNow - startTime).TotalSeconds} seconds");
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
		const int MaxConfirmations = 1400;

		void ListTransactions()
		{
			var removeFromWalletEntries = new HashSet<uint256>(_WalletEntries.Keys);

			int count = 100;
			int skip = 0;
			HashSet<uint256> processedTransacions = new HashSet<uint256>();
			int updatedConfirmationGap = -1;

			//If this one is true after asking for a batch, just update every "removeFromWalletEntries" transactions by updatedConfirmationGap
			bool canMakeSimpleUpdate = false;

			bool transactionTooOld = false;
			while(true)
			{
				updatedConfirmationGap = -1;
				canMakeSimpleUpdate = false;
				var result = _RPCClient.SendCommand("listtransactions", "*", count, skip, true);
				skip += count;
				var transactions = (JArray)result.Result;
				if(transactions.Count < count)
				{
					updatedConfirmationGap = 0;
					canMakeSimpleUpdate = false;
				}

				var batch = _RPCClient.PrepareBatch();
				var fetchingTransactions = new List<Tuple<RPCWalletEntry, Task<Transaction>>>();
				foreach(var obj in transactions)
				{

					var entry = new RPCWalletEntry();
					entry.Confirmations = obj["confirmations"] == null ? 0 : (int)obj["confirmations"];
					if(entry.Confirmations >= MaxConfirmations)
					{
						transactionTooOld = true;
						break;
					}
					entry.TransactionId = new uint256((string)obj["txid"]);
					if(!processedTransacions.Add(entry.TransactionId))
						continue;
					removeFromWalletEntries.Remove(entry.TransactionId);

					RPCWalletEntry existing;
					if(_WalletEntries.TryGetValue(entry.TransactionId, out existing))
					{
						var confirmationGap = entry.Confirmations - existing.Confirmations;
						if(entry.Confirmations == 0)
						{
							updatedConfirmationGap = 0;
							canMakeSimpleUpdate = false;
						}
						if(updatedConfirmationGap != -1 && updatedConfirmationGap != confirmationGap)
							canMakeSimpleUpdate = false;

						if(updatedConfirmationGap == -1)
						{
							canMakeSimpleUpdate = true;
							updatedConfirmationGap = confirmationGap;
						}

						existing.Confirmations = entry.Confirmations;
						entry = existing;
					}

					if(entry.Transaction == null)
					{
						fetchingTransactions.Add(Tuple.Create(entry, FetchTransactionAsync(batch, entry.TransactionId)));
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
				if(transactions.Count < count || transactionTooOld || canMakeSimpleUpdate)
					break;
			}

			if(canMakeSimpleUpdate)
			{
				foreach(var tx in removeFromWalletEntries.ToList())
				{
					RPCWalletEntry entry = null;
					if(_WalletEntries.TryGetValue(tx, out entry))
					{
						if(entry.Confirmations != 0)
						{
							entry.Confirmations += updatedConfirmationGap;
							if(entry.Confirmations <= MaxConfirmations)
								removeFromWalletEntries.Remove(entry.TransactionId);
						}
					}
				}
			}

			foreach(var remove in removeFromWalletEntries)
			{
				RPCWalletEntry opt;
				if(_WalletEntries.TryRemove(remove, out opt))
				{
					RemoveTxByScriptId(opt);
				}
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
