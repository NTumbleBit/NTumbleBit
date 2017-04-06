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
			public TrustedBroadcastRequest Request
			{
				get; set;
			}
		}

		public RPCTrustedBroadcastService(RPCClient rpc, IBroadcastService innerBroadcast, IBlockExplorerService explorer, IRepository repository)
		{
			if(rpc == null)
				throw new ArgumentNullException(nameof(rpc));
			if(innerBroadcast == null)
				throw new ArgumentNullException(nameof(innerBroadcast));
			if(repository == null)
				throw new ArgumentNullException(nameof(repository));
			if(explorer == null)
				throw new ArgumentNullException(nameof(explorer));
			_Repository = repository;
			_RPCClient = rpc;
			_Broadcaster = innerBroadcast;
			TrackPreviousScriptPubKey = true;
			_BlockExplorer = explorer;
		}

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

		public void Broadcast(string label, TrustedBroadcastRequest broadcast)
		{
			if(broadcast == null)
				throw new ArgumentNullException(nameof(broadcast));
			var address = broadcast.PreviousScriptPubKey.GetDestinationAddress(RPCClient.Network);
			if(address == null)
				throw new NotSupportedException("ScriptPubKey to track not supported");
			if(TrackPreviousScriptPubKey)
				RPCClient.ImportAddress(address, label + " (PreviousScriptPubKey)", false);
			var height = GetBlockCountAsync().GetAwaiter().GetResult();
			var record = new Record();
			record.Label = label;
			//3 days expiration
			record.Expiration = height + (int)(TimeSpan.FromDays(3).Ticks / Network.Main.Consensus.PowTargetSpacing.Ticks);
			record.Request = broadcast;
			AddBroadcast(record);
			if(height < broadcast.BroadcastAt.Height)
				return;
			_Broadcaster.Broadcast(record.Label, broadcast.Transaction);
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
			var height = GetBlockCountAsync().GetAwaiter().GetResult();
			List<Transaction> broadcasted = new List<Transaction>();
			foreach(var broadcast in GetRequests())
			{
				if(height < broadcast.Request.BroadcastAt.Height)
					continue;

				foreach(var tx in GetReceivedTransactions(broadcast.Request.PreviousScriptPubKey))
				{
					foreach(var coin in tx.Outputs.AsCoins())
					{
						if(coin.ScriptPubKey == broadcast.Request.PreviousScriptPubKey)
						{
							var transaction = broadcast.Request.ReSign(coin);
							if(_Broadcaster.Broadcast(broadcast.Label, transaction))
								broadcasted.Add(transaction);
						}
					}
				}

				var remove = height >= broadcast.Expiration;
				if(remove)
					Repository.Delete<Record>("TrustedBroadcasts", broadcast.Request.Transaction.GetHash().ToString());
			}
			return broadcasted.ToArray();
		}

		private async Task<int> GetBlockCountAsync()
		{
			var blockCount = RPCClient.GetBlockCountAsync();
			var rpcExplorer = BlockExplorer as RPCBlockExplorerService;
			if(rpcExplorer == null)
				return await blockCount.ConfigureAwait(false);

			var bestBlockHash = await RPCClient.GetBestBlockHashAsync().ConfigureAwait(false);
			if(lastBlock != bestBlockHash)
				rpcExplorer.InvalidCachedTransactions();
			lastBlock = bestBlockHash;
			return await blockCount.ConfigureAwait(false);
		}

		uint256 lastBlock = null;
		private readonly IBlockExplorerService _BlockExplorer;
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

			return BlockExplorer.GetTransactions(scriptPubKey, false)
				.Where(t => t.Transaction.Outputs.Any(o => o.ScriptPubKey == scriptPubKey))
				.Select(t => t.Transaction)
				.ToArray();
		}
	}


}
