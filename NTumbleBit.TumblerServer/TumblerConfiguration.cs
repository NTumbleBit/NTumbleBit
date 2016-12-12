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
			ClassicTumblerParameters = new ClassicTumblerParameters();
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

		public ClassicTumblerParameters ClassicTumblerParameters
		{
			get; set;
		}

		public ClassicTumblerParameters CreateClassicTumblerParameters()
		{
			var clone = Serializer.Clone(ClassicTumblerParameters);
			clone.ServerKey = TumblerKey.PubKey;
			clone.VoucherKey = VoucherKey.PubKey;
			return clone;
		}
	}
}
