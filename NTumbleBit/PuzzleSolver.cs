using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
    public class PuzzleSolver
    {
		private readonly int _RealPuzzleCount;
		private readonly int _FakePuzzleCount;

		public PuzzleSolver(int realPuzzleCount, int fakePuzzleCount)
		{
			if(realPuzzleCount <= 0 || fakePuzzleCount <= 0)
				throw new ArgumentOutOfRangeException();
			this._RealPuzzleCount = realPuzzleCount;
			this._FakePuzzleCount = fakePuzzleCount;
		}

		public int RealPuzzleCount
		{
			get
			{
				return _RealPuzzleCount;
			}
		}

		public int FakePuzzleCount
		{
			get
			{
				return _FakePuzzleCount;
			}
		}

		public int TotalPuzzleCount
		{
			get
			{
				return _FakePuzzleCount + _RealPuzzleCount;
			}
		}
	}
}
