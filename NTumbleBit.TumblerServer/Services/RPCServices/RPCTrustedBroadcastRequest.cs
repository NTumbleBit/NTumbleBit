using NBitcoin;
using NBitcoin.RPC;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#if !CLIENT
namespace NTumbleBit.TumblerServer.Services.RPCServices
#else
namespace NTumbleBit.Client.Tumbler.Services.RPCServices
#endif
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

			public uint Correlation
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

		public void Broadcast(int cycleStart, TransactionType transactionType, uint correlation, TrustedBroadcastRequest broadcast)
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
			var expirationBase = Math.Max(height, broadcast.BroadcastAt.Height);
			expirationBase = Math.Max(expirationBase, broadcast.Transaction.LockTime.Height);
			record.Expiration = expirationBase + (int)(TimeSpan.FromDays(3).Ticks / Network.Main.Consensus.PowTargetSpacing.Ticks);

			record.Request = broadcast;
			record.TransactionType = transactionType;
			record.Cycle = cycleStart;
			record.Correlation = correlation;
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
			HashSet<uint256> knownBroadcastedSet = new HashSet<uint256>(knownBroadcasted ?? new uint256[0]);

			List<Transaction> broadcasted = new List<Transaction>();
			foreach(var broadcast in GetRequests())
			{
				if(height < broadcast.Request.BroadcastAt.Height)
					continue;

				if(broadcast.Request.PreviousScriptPubKey == null)
				{
					var transaction = broadcast.Request.Transaction;
					var txHash = transaction.GetHash();
					_Tracker.TransactionCreated(broadcast.Cycle, broadcast.TransactionType, txHash, broadcast.Correlation);
					if(!knownBroadcastedSet.Contains(txHash) &&
									_Broadcaster.Broadcast(transaction))
					{
						broadcasted.Add(transaction);
					}
					knownBroadcastedSet.Add(txHash);
				}
				else
				{
					foreach(var tx in GetReceivedTransactions(broadcast.Request.PreviousScriptPubKey))
					{
						foreach(var coin in tx.Outputs.AsCoins())
						{
							if(coin.ScriptPubKey == broadcast.Request.PreviousScriptPubKey)
							{
								var transaction = broadcast.Request.ReSign(coin);
								var txHash = transaction.GetHash();
								_Tracker.TransactionCreated(broadcast.Cycle, broadcast.TransactionType, txHash, broadcast.Correlation);

								if(!knownBroadcastedSet.Contains(txHash) &&
									_Broadcaster.Broadcast(transaction))
								{
									broadcasted.Add(transaction);
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
			return broadcasted.ToArray();
		}

		public TrustedBroadcastRequest GetKnownTransaction(uint256 txId)
		{
			return Repository.Get<Record>("TrustedBroadcasts", txId.ToString())?.Request;
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
				BlockExplorer.GetTransactions(scriptPubKey, false)
				.Where(t => t.Transaction.Outputs.Any(o => o.ScriptPubKey == scriptPubKey))
				.Select(t => t.Transaction)
				.ToArray();
		}
	}


}
