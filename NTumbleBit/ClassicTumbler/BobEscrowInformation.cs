using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
    public class BobEscrowInformation
    {
		public PubKey EscrowKey
		{
			get; set;
		}
		public PuzzleSolution SignedVoucher
		{
			get; set;
		}
	}
}
