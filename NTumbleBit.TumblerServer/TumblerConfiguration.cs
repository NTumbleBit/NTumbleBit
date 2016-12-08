using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin.RPC;
using NTumbleBit.ClassicTumbler;

namespace NTumbleBit.TumblerServer
{
    public class TumblerConfiguration
    {
		public TumblerConfiguration()
		{			
			CycleGenerator = new OverlappedCycleGenerator();
		}
		public RsaKey TumblerKey
		{
			get; set;
		}

		public RsaKey VoucherKey
		{
			get; set;
		}

		public ExtKey Seed
		{
			get; set;
		}
		public Network Network
		{
			get; set;
		}
		public RPCClient RPCClient
		{
			get;
			set;
		}
		public ClassicTumblerRepository Repository
		{
			get; set;
		}
		public OverlappedCycleGenerator CycleGenerator
		{
			get;
			set;
		}

		public ClassicTumblerParameters CreateClassicTumblerParameters()
		{
			return new ClassicTumblerParameters(TumblerKey.PubKey, VoucherKey.PubKey)
			{
				CycleGenerator = CycleGenerator
			};
		}
	}
}
