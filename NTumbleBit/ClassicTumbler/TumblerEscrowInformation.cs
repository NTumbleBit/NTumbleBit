using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
    public class TumblerEscrowInformation
    {
		public PubKey Redeem
		{
			get; set;
		}
		public PubKey Escrow
		{
			get; set;
		}
	}
}
