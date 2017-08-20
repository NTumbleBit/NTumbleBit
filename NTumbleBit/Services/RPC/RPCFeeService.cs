using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;

namespace NTumbleBit.Services.RPC
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

		class RateCache
		{
			public FeeRate Rate;
			public DateTimeOffset Time;
		}
		RateCache _Cache;
		TimeSpan CacheExpiration = TimeSpan.FromSeconds(60 * 5);
		public FeeRate GetFeeRate()
		{
			var cache = _Cache;
			if(cache != null && (DateTime.UtcNow - cache.Time) < CacheExpiration)
			{
				return cache.Rate;
			}
			var rate = _RPCClient.TryEstimateFeeRate(1) ??
				   _RPCClient.TryEstimateFeeRate(2) ??
				   _RPCClient.TryEstimateFeeRate(3) ??
				   FallBackFeeRate;
			if(rate == null)
				throw new FeeRateUnavailableException("The fee rate is unavailable");
			if(rate < MinimumFeeRate)
				rate = MinimumFeeRate;
			cache = new RateCache();
			cache.Rate = rate;
			cache.Time = DateTimeOffset.UtcNow;
			_Cache = cache;
			return rate;
		}
	}
}
