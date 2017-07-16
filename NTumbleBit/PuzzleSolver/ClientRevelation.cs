using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
	public class ClientRevelation : IBitcoinSerializable
	{
		public ClientRevelation()
		{

		}
		public ClientRevelation(int[] fakeIndexes, PuzzleSolution[] solutions)
		{
			FakeIndexes = fakeIndexes;
			Solutions = solutions;
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


		PuzzleSolution[] _Solutions;
		public PuzzleSolution[] Solutions
		{
			get
			{
				return _Solutions;
			}
			set
			{
				_Solutions = value;
			}
		}

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWrite(ref _FakeIndexes);
			stream.ReadWrite(ref _Solutions);
		}
	}
}
