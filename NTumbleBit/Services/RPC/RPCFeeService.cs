using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using System.Threading;

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

		FeeRate _CachedValue;
		DateTimeOffset _CachedValueTime;
		TimeSpan CacheExpiration = TimeSpan.FromSeconds(60 * 5);
		public async Task<FeeRate> GetFeeRateAsync()
		{
			if(DateTimeOffset.UtcNow - _CachedValueTime > CacheExpiration)
			{
				var rate = await FetchRateAsync();
				_CachedValue = rate;
				_CachedValueTime = DateTimeOffset.UtcNow;
				return rate;
			}
			else
			{
				return _CachedValue;
			}
		}

		private async Task<FeeRate> FetchRateAsync()
		{
			var rate = await _RPCClient.TryEstimateFeeRateAsync(1).ConfigureAwait(false) ??
							   await _RPCClient.TryEstimateFeeRateAsync(2).ConfigureAwait(false) ??
							   await _RPCClient.TryEstimateFeeRateAsync(3).ConfigureAwait(false) ??
							   FallBackFeeRate;
			if(rate == null)
				throw new FeeRateUnavailableException("The fee rate is unavailable");
			if(rate < MinimumFeeRate)
				rate = MinimumFeeRate;
			return rate;
		}
	}
}
