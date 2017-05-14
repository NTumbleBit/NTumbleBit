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
				throw new ArgumentNullException(nameof(rpc));
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
		public FeeRate FallBackFeeRate
		{
			get; set;
		}
		public FeeRate MinimumFeeRate
		{
			get; set;
		}
		public FeeRate GetFeeRate()
		{
			var rate = _RPCClient.TryEstimateFeeRate(1) ??
				   _RPCClient.TryEstimateFeeRate(2) ??
				   _RPCClient.TryEstimateFeeRate(3) ??
				   FallBackFeeRate;
			if(rate == null)
				throw new FeeRateUnavailableException("The fee rate is unavailable");
			if(rate < MinimumFeeRate)
				rate = MinimumFeeRate;
			return rate;
		}
	}
}
