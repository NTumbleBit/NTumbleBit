using NBitcoin;
using NBitcoin.RPC;
using NTumbleBit.Logging;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace NTumbleBit.Services.RPC
{
	public class RPCTrustedBroadcastService : ITrustedBroadcastService
	{
		public class Record
		{
			public int Expiration
			{
				get; set;
			}
			public string Label
			{
				get;
				set;
			}

			public TransactionType TransactionType
			{
				get; set;
			}

			public int Cycle
			{
				get; set;
			}

			public TrustedBroadcastRequest Request
			{
				get; set;
			}

			public CorrelationId Correlation
			{
				get; set;
			}
		}

		public class TxToRecord
		{
			public uint256 RecordHash
			{
				get; set;
			}
			public Transaction Transaction
			{
				get; set;
			}
		}

		public RPCTrustedBroadcastService(RPCClient rpc, IBroadcastService innerBroadcast, IBlockExplorerService explorer, IRepository repository, RPCWalletCache cache, Tracker tracker)
		{
			if(rpc == null)
				throw new ArgumentNullException(nameof(rpc));
			if(innerBroadcast == null)
				throw new ArgumentNullException(nameof(innerBroadcast));
			if(repository == null)
				throw new ArgumentNullException(nameof(repository));
			if(explorer == null)
				throw new ArgumentNullException(nameof(explorer));
			if(tracker == null)
				throw new ArgumentNullException(nameof(tracker));
			if(cache == null)
				throw new ArgumentNullException(nameof(cache));
			_Repository = repository;
			_RPCClient = rpc;
			_Broadcaster = innerBroadcast;
			TrackPreviousScriptPubKey = true;
			_BlockExplorer = explorer;
			_Tracker = tracker;
			_Cache = cache;
		}

		private Tracker _Tracker;
		private IBroadcastService _Broadcaster;

		private readonly RPCClient _RPCClient;
		public RPCClient RPCClient
		{
			get
			{
				return _RPCClient;
			}
		}

		public bool TrackPreviousScriptPubKey
		{
			get; set;
		}

		public void Broadcast(int cycleStart, TransactionType transactionType, CorrelationId correlation, TrustedBroadcastRequest broadcast)
		{
			if(broadcast == null)
				throw new ArgumentNullException(nameof(broadcast));
			if(broadcast.Key != null && !broadcast.Transaction.Inputs.Any(i => i.PrevOut.IsNull))
				throw new InvalidOperationException("One of the input should be null");

			var address = broadcast.PreviousScriptPubKey?.GetDestinationAddress(RPCClient.Network);
			if(address != null && TrackPreviousScriptPubKey)
				RPCClient.ImportAddress(address, "", false);

			var height = _Cache.BlockCount;
			var record = new Record();
			//3 days expiration after now or broadcast date
			var expirationBase = Math.Max(height, broadcast.BroadcastableHeight);
			record.Expiration = expirationBase + (int)(TimeSpan.FromDays(3).Ticks / RPCClient.Network.Consensus.PowTargetSpacing.Ticks);

			record.Request = broadcast;
			record.TransactionType = transactionType;
			record.Cycle = cycleStart;
			record.Correlation = correlation;
			Logs.Broadcasters.LogInformation($"Planning to broadcast {record.TransactionType} of cycle {record.Cycle} on block {record.Request.BroadcastableHeight}");
			AddBroadcast(record);
		}
		

		private void AddBroadcast(Record broadcast)
		{
			Repository.UpdateOrInsert("TrustedBroadcasts", broadcast.Request.Transaction.GetHash().ToString(), broadcast, (o, n) => n);
		}

		private readonly IRepository _Repository;
		public IRepository Repository
		{
			get
			{
				return _Repository;
			}
		}

		public Record[] GetRequests()
		{
			var requests = Repository.List<Record>("TrustedBroadcasts");
			return requests.TopologicalSort(tx => requests.Where(tx2 => tx2.Request.Transaction.Outputs.Any(o => o.ScriptPubKey == tx.Request.PreviousScriptPubKey))).ToArray();
		}

		public Transaction[] TryBroadcast()
		{
			uint256[] b = null;
			return TryBroadcast(ref b);
		}
		public Transaction[] TryBroadcast(ref uint256[] knownBroadcasted)
		{
			var height = _Cache.BlockCount;

			DateTimeOffset startTime = DateTimeOffset.UtcNow;
			int totalEntries = 0;

			HashSet<uint256> knownBroadcastedSet = new HashSet<uint256>(knownBroadcasted ?? new uint256[0]);
			foreach(var confirmedTx in _Cache.GetEntries().Where(e => e.Confirmations > 6).Select(t => t.TransactionId))
			{
				knownBroadcastedSet.Add(confirmedTx);
			}

			List<Transaction> broadcasted = new List<Transaction>();
			var broadcasting = new List<Tuple<Record, Transaction, Task<bool>>>();

			foreach(var broadcast in GetRequests())
			{
				totalEntries++;
				if(broadcast.Request.PreviousScriptPubKey == null)
				{
					var transaction = broadcast.Request.Transaction;
					var txHash = transaction.GetHash();
					_Tracker.TransactionCreated(broadcast.Cycle, broadcast.TransactionType, txHash, broadcast.Correlation);
					RecordMaping(broadcast, transaction, txHash);

					if(!knownBroadcastedSet.Contains(txHash)
						&& broadcast.Request.IsBroadcastableAt(height))
					{
						broadcasting.Add(Tuple.Create(broadcast, transaction, _Broadcaster.BroadcastAsync(transaction)));
					}
					knownBroadcastedSet.Add(txHash);
				}
				else
				{
					foreach(var tx in GetReceivedTransactions(broadcast.Request.PreviousScriptPubKey)
						//Currently broadcasting transaction might have received transactions for PreviousScriptPubKey
						.Concat(broadcasting.ToArray().Select(b => b.Item2)))
					{
						foreach(var coin in tx.Outputs.AsCoins())
						{
							if(coin.ScriptPubKey == broadcast.Request.PreviousScriptPubKey)
							{
								bool cached;
								var transaction = broadcast.Request.ReSign(coin, out cached);
								var txHash = transaction.GetHash();
								if(!cached)
								{
									_Tracker.TransactionCreated(broadcast.Cycle, broadcast.TransactionType, txHash, broadcast.Correlation);
									RecordMaping(broadcast, transaction, txHash);
									AddBroadcast(broadcast);
								}

								if(!knownBroadcastedSet.Contains(txHash)
									&& broadcast.Request.IsBroadcastableAt(height))
								{
									broadcasting.Add(Tuple.Create(broadcast, transaction, _Broadcaster.BroadcastAsync(transaction)));
								}
								knownBroadcastedSet.Add(txHash);
							}
						}
					}
				}

				var remove = height >= broadcast.Expiration;
				if(remove)
					Repository.Delete<Record>("TrustedBroadcasts", broadcast.Request.Transaction.GetHash().ToString());
			}

			knownBroadcasted = knownBroadcastedSet.ToArray();

			foreach(var b in broadcasting)
			{
				if(b.Item3.GetAwaiter().GetResult())
				{
					LogBroadcasted(b.Item1);
					broadcasted.Add(b.Item2);
				}
			}

			Logs.Broadcasters.LogInformation($"Trusted Broadcaster is monitoring {totalEntries} entries in {(long)(DateTimeOffset.UtcNow - startTime).TotalSeconds} seconds");
			return broadcasted.ToArray();
		}

		private void LogBroadcasted(Record broadcast)
		{
			Logs.Broadcasters.LogInformation($"Broadcasted {broadcast.TransactionType} of cycle {broadcast.Cycle} planned on block {broadcast.Request.BroadcastableHeight}");
		}

		private void RecordMaping(Record broadcast, Transaction transaction, uint256 txHash)
		{
			var txToRecord = new TxToRecord()
			{
				RecordHash = broadcast.Request.Transaction.GetHash(),
				Transaction = transaction
			};
			Repository.UpdateOrInsert<TxToRecord>("TxToRecord", txHash.ToString(), txToRecord, (a, b) => a);
		}

		public TrustedBroadcastRequest GetKnownTransaction(uint256 txId)
		{
			var mapping = Repository.Get<TxToRecord>("TxToRecord", txId.ToString());
			if(mapping == null)
				return null;
			var record = Repository.Get<Record>("TrustedBroadcasts", mapping.RecordHash.ToString()).Request;
			if(record == null)
				return null;
			record.Transaction = mapping.Transaction;
			return record;
		}

		private readonly IBlockExplorerService _BlockExplorer;
		private readonly RPCWalletCache _Cache;

		public IBlockExplorerService BlockExplorer
		{
			get
			{
				return _BlockExplorer;
			}
		}


		public Transaction[] GetReceivedTransactions(Script scriptPubKey)
		{
			if(scriptPubKey == null)
				throw new ArgumentNullException(nameof(scriptPubKey));
			return
				BlockExplorer.GetTransactionsAsync(scriptPubKey, false).GetAwaiter().GetResult()
				.Where(t => t.Transaction.Outputs.Any(o => o.ScriptPubKey == scriptPubKey))
				.Select(t => t.Transaction)
				.ToArray();
		}
	}


}
