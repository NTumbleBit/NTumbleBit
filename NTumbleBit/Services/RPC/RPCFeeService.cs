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

		class RateCache
		{
			public FeeRate Rate;
			public DateTimeOffset Time;
			public TaskCompletionSource<bool> _Refreshing;
			object l = new object();
			public Task<bool> WaitRefreshed()
			{
				lock(l)
				{
					if(_Refreshing == null)
					{
						_Refreshing = new TaskCompletionSource<bool>();
						return Task.FromResult(false);
					}
					else
						return _Refreshing.Task;
				}
			}

			internal void Done()
			{
				lock(l)
				{
					_Refreshing.TrySetResult(true);
					_Refreshing = null;
				}
			}
		}
		RateCache _Cache = new RateCache();
		TimeSpan CacheExpiration = TimeSpan.FromSeconds(60 * 5);
		public async Task<FeeRate> GetFeeRateAsync()
		{
			if(_Cache != null && (DateTime.UtcNow - _Cache.Time) < CacheExpiration)
			{
				return _Cache.Rate;
			}
			if(await _Cache.WaitRefreshed().ConfigureAwait(false))
				return _Cache.Rate;

			var rate = await _RPCClient.TryEstimateFeeRateAsync(1).ConfigureAwait(false) ??
				   await _RPCClient.TryEstimateFeeRateAsync(2).ConfigureAwait(false) ??
				   await _RPCClient.TryEstimateFeeRateAsync(3).ConfigureAwait(false) ??
				   FallBackFeeRate;
			if(rate == null)
				throw new FeeRateUnavailableException("The fee rate is unavailable");
			if(rate < MinimumFeeRate)
				rate = MinimumFeeRate;
			_Cache.Rate = rate;
			_Cache.Time = DateTimeOffset.UtcNow;
			_Cache.Done();
			return rate;
		}
	}
}
