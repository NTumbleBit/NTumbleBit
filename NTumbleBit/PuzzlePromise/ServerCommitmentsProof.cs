using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
    public class ServerCommitmentsProof
	{
		public ServerCommitmentsProof(PuzzleSolution[] solutions, Quotient[] quotients)
		{
			if(solutions == null)
				throw new ArgumentNullException("solutions");
			if(quotients == null)
				throw new ArgumentNullException("quotients");
			_FakeSolutions = solutions;
			_Quotients = quotients;
		}

		readonly PuzzleSolution[] _FakeSolutions;
		public PuzzleSolution[] FakeSolutions
		{
			get
			{
				return _FakeSolutions;
			}
		}


		private readonly Quotient[] _Quotients;
		public Quotient[] Quotients
		{
			get
			{
				return _Quotients;
			}
		}
    }
}
