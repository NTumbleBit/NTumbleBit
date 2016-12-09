using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;

#if !CLIENT
namespace NTumbleBit.TumblerServer.Services.RPCServices
#else
namespace NTumbleBit.Client.Tumbler.Services.RPCServices
#endif
{
	public class RPCFeeService : IFeeService
	{
		public RPCFeeService(RPCClient rpc)
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
		public FeeRate GetFeeRate()
		{
			return _RPCClient.TryEstimateFeeRate(1) ?? new FeeRate(Money.Satoshis(50), 1);
		}
	}
}
