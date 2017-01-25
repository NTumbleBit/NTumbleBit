using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
	public class SolverParameters
	{
		public SolverParameters()
		{
			FakePuzzleCount = 285;
			RealPuzzleCount = 15;
		}

		public SolverParameters(RsaPubKey serverKey) : this()
		{
			if(serverKey == null)
				throw new ArgumentNullException(nameof(serverKey));
			ServerKey = serverKey;
		}


		public RsaPubKey ServerKey
		{
			get; set;
		}
		public int FakePuzzleCount
		{
			get; set;
		}
		public int RealPuzzleCount
		{
			get; set;
		}

		public int GetTotalCount()
		{
			return RealPuzzleCount + FakePuzzleCount;
		}		
	}
}
