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
		public RPCBroadcastService(RPCClient rpc)
		{
			if(rpc == null)
				throw new ArgumentNullException("rpc");
			_RPCClient = rpc;
		}

		private readonly RPCClient _RPCClient;
		public RPCClient RPCClient
		{
			get
			{
				return _RPCClient;
			}
		}

		public Transaction[] TryBroadcast()
		{
			List<Transaction> broadcasted = new List<Transaction>();
			foreach(var tx in _Transactions.ToList())
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
				_Transactions.Remove(tx);
				return true;
			}
			catch(RPCException ex)
			{
				if(ex.RPCResult == null || ex.RPCResult.Error == null)
				{
					//TODO: LOG? RETRY?
					return false;
				}
				var error = ex.RPCResult.Error.Message;
				if(!error.EndsWith("non-final", StringComparison.OrdinalIgnoreCase))
				{
					if(error.EndsWith("bad-txns-inputs-spent", StringComparison.OrdinalIgnoreCase))
					{
						_Transactions.Remove(tx);
					}
					else if(!error.EndsWith("txn-mempool-conflict", StringComparison.OrdinalIgnoreCase))
					{
						//TODO: LOG? RETRY?
						return false;
					}
				}
			}
			return false;
		}

		List<Transaction> _Transactions = new List<Transaction>();
		public bool Broadcast(Transaction transaction)
		{
			_Transactions.Add(transaction);
			return TryBroadcastCore(transaction);
		}
	}
}
