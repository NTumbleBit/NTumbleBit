using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
    public class SignaturesRequest
    {
		public uint256[] Hashes
		{
			get; set;
		}
		public uint256 FakeIndexesHash
		{
			get; set;
		}
	}
}
