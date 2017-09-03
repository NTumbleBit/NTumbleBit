using NBitcoin.RPC;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NTumbleBit.Logging;

namespace NTumbleBit.Services.RPC
{
	public class RPCBroadcastService : IBroadcastService
	{
		public class Record
		{
			public int Expiration
			{
				get; set;
			}
			public Transaction Transaction
			{
				get; set;
			}
		}
		RPCWalletCache _Cache;
		public RPCBroadcastService(RPCClient rpc, RPCWalletCache cache, IRepository repository)
		{
			if(rpc == null)
				throw new ArgumentNullException(nameof(rpc));
			if(repository == null)
				throw new ArgumentNullException(nameof(repository));
			_RPCClient = rpc;
			_Repository = repository;
			_Cache = cache;
			_BlockExplorerService = new RPCBlockExplorerService(rpc, cache, repository);
			_RPCBatch = new RPCBatch<bool>(_RPCClient);
		}

		public TimeSpan BatchInterval
		{
			get
			{
				return _RPCBatch.BatchInterval;
			}
			set
			{
				_RPCBatch.BatchInterval = value;
			}
		}


		private readonly RPCBlockExplorerService _BlockExplorerService;
		private readonly RPCBatch<bool> _RPCBatch;

		public RPCBlockExplorerService BlockExplorerService
		{
			get
			{
				return _BlockExplorerService;
			}
		}


		private readonly IRepository _Repository;
		public IRepository Repository
		{
			get
			{
				return _Repository;
			}
		}

		private readonly RPCClient _RPCClient;
		public RPCClient RPCClient
		{
			get
			{
				return _RPCClient;
			}
		}

		public Record[] GetTransactions()
		{
			var transactions = Repository.List<Record>("Broadcasts");
			foreach(var tx in transactions)
				tx.Transaction.CacheHashes();

			var txByTxId = transactions.ToDictionary(t => t.Transaction.GetHash());
			var dependsOn = transactions.Select(t => new
			{
				Tx = t,
				Depends = t.Transaction.Inputs.Select(i => i.PrevOut)
											  .Where(o => txByTxId.ContainsKey(o.Hash))
											  .Select(o => txByTxId[o.Hash])
			})
			.ToDictionary(o => o.Tx, o => o.Depends.ToArray());
			return transactions.TopologicalSort(tx => dependsOn[tx]).ToArray();
		}
		public Transaction[] TryBroadcast()
		{
			uint256[] r = null;
			return TryBroadcast(ref r);
		}
		public Transaction[] TryBroadcast(ref uint256[] knownBroadcasted)
		{
			var startTime = DateTimeOffset.UtcNow;
			int totalEntries = 0;
			List<Transaction> broadcasted = new List<Transaction>();
			var broadcasting = new List<Tuple<Transaction, Task<bool>>>();
			HashSet<uint256> knownBroadcastedSet = new HashSet<uint256>(knownBroadcasted ?? new uint256[0]);
			int height = _Cache.BlockCount;
			foreach(var obj in _Cache.GetEntries())
			{
				if(obj.Confirmations > 0)
					knownBroadcastedSet.Add(obj.TransactionId);
			}

			foreach(var tx in GetTransactions())
			{
				totalEntries++;
				if(!knownBroadcastedSet.Contains(tx.Transaction.GetHash()))
				{
					broadcasting.Add(Tuple.Create(tx.Transaction, TryBroadcastCoreAsync(tx, height)));
				}
				knownBroadcastedSet.Add(tx.Transaction.GetHash());
			}

			knownBroadcasted = knownBroadcastedSet.ToArray();

			foreach(var broadcast in broadcasting)
			{
				if(broadcast.Item2.GetAwaiter().GetResult())
					broadcasted.Add(broadcast.Item1);
			}

			Logs.Broadcasters.LogInformation($"Broadcasted {broadcasted.Count} transaction(s), monitoring {totalEntries} entries in {(long)(DateTimeOffset.UtcNow - startTime).TotalSeconds} seconds");
			return broadcasted.ToArray();
		}
		
		private async Task<bool> TryBroadcastCoreAsync(Record tx, int currentHeight)
		{
			bool remove = false;
			try
			{
				remove = currentHeight >= tx.Expiration;

				//Happens when the caller does not know the previous input yet
				if(tx.Transaction.Inputs.Count == 0 || tx.Transaction.Inputs[0].PrevOut.Hash == uint256.Zero)
					return false;

				bool isFinal = tx.Transaction.IsFinal(DateTimeOffset.UtcNow, currentHeight + 1);
				if(!isFinal || IsDoubleSpend(tx.Transaction))
					return false;

				try
				{
					await _RPCBatch.WaitTransactionAsync(async batch =>
					{
						await batch.SendRawTransactionAsync(tx.Transaction).ConfigureAwait(false);
						return true;
					}).ConfigureAwait(false);

					_Cache.ImportTransaction(tx.Transaction, 0);
					Logs.Broadcasters.LogInformation($"Broadcasted {tx.Transaction.GetHash()}");
					return true;
				}
				catch(RPCException ex)
				{
					if(ex.RPCResult == null || ex.RPCResult.Error == null)
					{
						return false;
					}
					var error = ex.RPCResult.Error.Message;
					if(ex.RPCResult.Error.Code != RPCErrorCode.RPC_TRANSACTION_ALREADY_IN_CHAIN &&
					   !error.EndsWith("bad-txns-inputs-spent", StringComparison.OrdinalIgnoreCase) &&
					   !error.EndsWith("txn-mempool-conflict", StringComparison.OrdinalIgnoreCase) &&
					   !error.EndsWith("Missing inputs", StringComparison.OrdinalIgnoreCase))
					{
						remove = false;
					}
				}
				return false;
			}
			finally
			{
				if(remove)
					RemoveRecord(tx);
			}
		}

		private bool IsDoubleSpend(Transaction tx)
		{
			var spentInputs = new HashSet<OutPoint>(tx.Inputs.Select(txin => txin.PrevOut));
			foreach(var entry in _Cache.GetEntries())
			{
				if(entry.Confirmations > 0)
				{
					var walletTransaction = _Cache.GetTransaction(entry.TransactionId);
					foreach(var input in walletTransaction.Inputs)
					{
						if(spentInputs.Contains(input.PrevOut))
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		private void RemoveRecord(Record tx)
		{
			Repository.Delete<Record>("Broadcasts", tx.Transaction.GetHash().ToString());
			Repository.UpdateOrInsert<Transaction>("CachedTransactions", tx.Transaction.GetHash().ToString(), tx.Transaction, (a, b) => a);
		}

		public Task<bool> BroadcastAsync(Transaction transaction)
		{
			var record = new Record();
			record.Transaction = transaction;
			var height = _Cache.BlockCount;
			//3 days expiration
			record.Expiration = height + (int)(TimeSpan.FromDays(3).Ticks / Network.Main.Consensus.PowTargetSpacing.Ticks);
			Repository.UpdateOrInsert<Record>("Broadcasts", transaction.GetHash().ToString(), record, (o, n) => o);
			return TryBroadcastCoreAsync(record, height);
		}

		public Transaction GetKnownTransaction(uint256 txId)
		{
			return Repository.Get<Record>("Broadcasts", txId.ToString())?.Transaction ??
				   Repository.Get<Transaction>("CachedTransactions", txId.ToString());
		}
	}
}
