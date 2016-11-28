using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
    public class ClientRevelation
    {
		public ClientRevelation(int[] indexes, uint256[] salts)
		{
			FakeIndexes = indexes;
			Salts = salts;
			if(indexes.Length != salts.Length)
				throw new ArgumentException("Indexes and Salts array should be of the same length");
		}
		public int[] FakeIndexes
		{
			get;
			private set;
		}
		public uint256[] Salts
		{
			get;
			private set;
		}
	}
}
