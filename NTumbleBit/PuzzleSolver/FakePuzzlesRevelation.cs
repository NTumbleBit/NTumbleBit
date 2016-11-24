using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
	public class FakePuzzlesRevelation
	{
		public FakePuzzlesRevelation(int[] indexes, PuzzleSolution[] solutions)
		{
			Indexes = indexes;
			Solutions = solutions;
		}
		public int[] Indexes
		{
			get; set;
		}

		public PuzzleSolution[] Solutions
		{
			get; set;
		}
	}
}
