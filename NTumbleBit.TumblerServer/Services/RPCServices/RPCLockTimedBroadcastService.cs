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
	public class RPCLockTimedBroadcastService : ILockTimedBroadcastService
    {
		public RPCLockTimedBroadcastService(RPCClient rpc)
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
				try
				{
					RPCClient.SendRawTransaction(tx);
					broadcasted.Add(tx);
					_Transactions.Remove(tx);
				}
				catch(RPCException ex)
				{
					if(ex.RPCResult == null || ex.RPCResult.Error == null)
					{
						//TODO: LOG? RETRY?
						continue;
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
							continue;
						}
					}
				}
			}
			return broadcasted.ToArray();
		}

		List<Transaction> _Transactions = new List<Transaction>();
		public void BroadcastLater(Transaction transaction)
		{
			_Transactions.Add(transaction);
		}
	}
}
