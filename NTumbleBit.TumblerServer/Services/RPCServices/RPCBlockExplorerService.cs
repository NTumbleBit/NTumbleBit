using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.TumblerServer.Services.RPCServices
{
	public class RPCBlockExplorerService : IBlockExplorerService
	{
		public RPCBlockExplorerService(RPCClient client)
		{
			if(client == null)
				throw new ArgumentNullException("client");
			_RPCClient = client;
		}

		private readonly RPCClient _RPCClient;
		public RPCClient RPCClient
		{
			get
			{
				return _RPCClient;
			}
		}
		public int GetCurrentHeight()
		{
			return RPCClient.GetBlockCount();
		}
	}
}
