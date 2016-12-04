using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
	public class ClientRevelation
	{
		public ClientRevelation()
		{

		}
		public ClientRevelation(int[] indexes, uint256 indexesSalt, uint256[] salts)
		{
			FakeIndexes = indexes;
			Salts = salts;
			IndexesSalt = indexesSalt;
			if(indexes.Length != salts.Length)
				throw new ArgumentException("Indexes and Salts array should be of the same length");
		}
		public uint256 IndexesSalt
		{
			get; set;
		}
		public int[] FakeIndexes
		{
			get;
			set;
		}
		public uint256[] Salts
		{
			get;
			set;
		}
	}
}
