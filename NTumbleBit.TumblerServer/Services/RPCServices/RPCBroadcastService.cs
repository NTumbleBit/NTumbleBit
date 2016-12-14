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
		public RPCBroadcastService(RPCClient rpc, IRepository repository)
		{
			if(rpc == null)
				throw new ArgumentNullException("rpc");
			if(repository == null)
				throw new ArgumentNullException("repository");
			_RPCClient = rpc;
			_Repository = repository;
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

		public Transaction[] GetTransactions()
		{
			var transactions = Repository.List<Transaction>("Broadcasts");
			foreach(var tx in transactions)
				tx.CacheHashes();
			return Utils.TopologicalSort(transactions,
				tx => transactions.Where(tx2 => tx.Inputs.Any(input => input.PrevOut.Hash == tx2.GetHash()))).ToArray();
		}

		public Transaction[] TryBroadcast()
		{
			List<Transaction> broadcasted = new List<Transaction>();
			foreach(var tx in GetTransactions())
			{
				if(TryBroadcastCore(tx))
				{
					broadcasted.Add(tx);
				}
			}
			return broadcasted.ToArray();
		}

		bool TryBroadcastCore(Transaction tx)
		{
			try
			{
				RPCClient.SendRawTransaction(tx);
				return true;
			}
			catch(RPCException ex)
			{
				if(ex.RPCResult == null || ex.RPCResult.Error == null)
				{
					return false;
				}
				var error = ex.RPCResult.Error.Message;
				if(!error.EndsWith("non-final", StringComparison.OrdinalIgnoreCase))
				{
					if(error.EndsWith("bad-txns-inputs-spent", StringComparison.OrdinalIgnoreCase))
					{
					}
					else if(!error.EndsWith("txn-mempool-conflict", StringComparison.OrdinalIgnoreCase))
					{
					}
				}
			}
			return false;
		}

		public bool Broadcast(Transaction transaction)
		{
			Repository.UpdateOrInsert<Transaction>("Broadcasts", transaction.GetHash().ToString(), transaction, (o, n) => n);
			return TryBroadcastCore(transaction);
		}
	}
}
