using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

#if !CLIENT
namespace NTumbleBit.TumblerServer.Services.RPCServices
#else
namespace NTumbleBit.Client.Tumbler.Services.RPCServices
#endif
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
		public RPCBroadcastService(RPCClient rpc, IRepository repository)
		{
			if(rpc == null)
				throw new ArgumentNullException(nameof(rpc));
			if(repository == null)
				throw new ArgumentNullException(nameof(repository));
			_RPCClient = rpc;
			_Repository = repository;
			_BlockExplorerService = new RPCBlockExplorerService(rpc);
		}


		private readonly RPCBlockExplorerService _BlockExplorerService;
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
			return transactions.TopologicalSort(tx => transactions.Where(tx2 => tx.Transaction.Inputs.Any<TxIn>(input => input.PrevOut.Hash == tx2.Transaction.GetHash()))).ToArray();
		}

		public Transaction[] TryBroadcast()
		{
			List<Transaction> broadcasted = new List<Transaction>();
			int height = RPCClient.GetBlockCount();
			foreach(var tx in GetTransactions())
			{
				if(TryBroadcastCore(tx, height))
				{
					broadcasted.Add(tx.Transaction);
				}
			}
			return broadcasted.ToArray();
		}

		private bool TryBroadcastCore(Record tx, int currentHeight)
		{
			bool remove = currentHeight >= tx.Expiration;

			//Make the broadcast a bit faster
			var isNonFinal =
				tx.Transaction.LockTime.IsHeightLock &&
				tx.Transaction.LockTime.Height > currentHeight &&
				tx.Transaction.Inputs.Any(i => i.Sequence != Sequence.Final);

			if(isNonFinal)
				return false;

			try
			{
				RPCClient.SendRawTransaction(tx.Transaction);
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

			if(remove)
			{
				Repository.Delete<Record>("Broadcasts", tx.Transaction.GetHash().ToString());
				Repository.UpdateOrInsert("BroadcastsArchived", tx.Transaction.GetHash().ToString(), tx, (a, b) => a);
			}
			return false;
		}

		public bool Broadcast(Transaction transaction)
		{
			var record = new Record();
			record.Transaction = transaction;
			var height = _RPCClient.GetBlockCount();
			//3 days expiration
			record.Expiration = height + (int)(TimeSpan.FromDays(3).Ticks / Network.Main.Consensus.PowTargetSpacing.Ticks);
			Repository.UpdateOrInsert<Record>("Broadcasts", transaction.GetHash().ToString(), record, (o, n) => o);
			return TryBroadcastCore(record, height);
		}
	}
}
