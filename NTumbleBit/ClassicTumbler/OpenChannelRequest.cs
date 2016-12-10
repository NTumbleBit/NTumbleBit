using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
    public class OpenChannelRequest
    {
		public PubKey EscrowKey
		{
			get; set;
		}
		public uint160 SignedVoucher
		{
			get; set;
		}
	}
}
