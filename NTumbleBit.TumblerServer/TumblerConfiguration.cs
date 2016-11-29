using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
	}
}
