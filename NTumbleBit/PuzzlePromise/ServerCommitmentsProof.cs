using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
    public class ServerCommitmentsProof
	{
		public ServerCommitmentsProof()
		{

		}
		public ServerCommitmentsProof(PuzzleSolution[] solutions, Quotient[] quotients)
		{
			if(solutions == null)
				throw new ArgumentNullException(nameof(solutions));
			if(quotients == null)
				throw new ArgumentNullException(nameof(quotients));
			FakeSolutions = solutions;
			Quotients = quotients;
		}

		public PuzzleSolution[] FakeSolutions
		{
			get; set;
		}


		public Quotient[] Quotients
		{
			get; set;
		}
	}
}
