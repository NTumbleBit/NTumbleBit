using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
    public class PuzzleSolver
    {
		public PuzzleSolver(PuzzleSolverParameters parameters)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			if(parameters.RealPuzzleCount <= 0 || parameters.FakePuzzleCount <= 0)
				throw new ArgumentOutOfRangeException();
			_Parameters = parameters;
		}


		private readonly PuzzleSolverParameters _Parameters;
		public PuzzleSolverParameters Parameters
		{
			get
			{
				return _Parameters;
			}
		}

		public int RealPuzzleCount
		{
			get
			{
				return Parameters.RealPuzzleCount;
			}
		}

		public int FakePuzzleCount
		{
			get
			{
				return Parameters.FakePuzzleCount;
			}
		}

		public int TotalPuzzleCount
		{
			get
			{
				return FakePuzzleCount + RealPuzzleCount;
			}
		}
	}
}
