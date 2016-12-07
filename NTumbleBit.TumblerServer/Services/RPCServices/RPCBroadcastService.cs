using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;

namespace NTumbleBit.TumblerServer.Services.RPCServices
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
		public void Broadcast(Transaction transaction)
		{
			_RPCClient.SendRawTransaction(transaction);
		}
	}
}
