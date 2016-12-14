using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
    public class UnsignedVoucherInformation
    {
		public PuzzleValue Puzzle
		{
			get; set;
		}
		public byte[] EncryptedSignature
		{
			get; set;
		}
		public uint160 Nonce
		{
			get; set;
		}
		public int CycleStart
		{
			get; set;
		}
	}
}
