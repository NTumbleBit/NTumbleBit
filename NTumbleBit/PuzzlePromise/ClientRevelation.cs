using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
	public class ClientRevelation : IBitcoinSerializable
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


		uint256 _IndexesSalt;
		public uint256 IndexesSalt
		{
			get
			{
				return _IndexesSalt;
			}
			set
			{
				_IndexesSalt = value;
			}
		}


		int[] _FakeIndexes;
		public int[] FakeIndexes
		{
			get
			{
				return _FakeIndexes;
			}
			set
			{
				_FakeIndexes = value;
			}
		}


		uint256[] _Salts;
		public uint256[] Salts
		{
			get
			{
				return _Salts;
			}
			set
			{
				_Salts = value;
			}
		}
		
		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWrite(ref _IndexesSalt);
			stream.ReadWrite(ref _FakeIndexes);
			stream.ReadWriteC(ref _Salts);
		}
	}
}
