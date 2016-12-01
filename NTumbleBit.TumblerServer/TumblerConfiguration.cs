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
		public RsaKey RsaKey
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

		public ClassicTumblerParameters TumblerParameters
		{
			get; set;
		}
	}
}
