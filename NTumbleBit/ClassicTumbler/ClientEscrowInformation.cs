using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
    public class ClientEscrowInformation
    {
		public PubKey EscrowKey
		{
			get; set;
		}
		public PubKey RedeemKey
		{
			get; set;
		}
		public int Cycle
		{
			get; set;
		}
		public PuzzleValue UnsignedVoucher
		{
			get; set;
		}
	}
}
