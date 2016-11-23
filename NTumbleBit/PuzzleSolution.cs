using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class PuzzleSolution
	{
		public PuzzleSolution(int index, byte[] solution)
		{
			Index = index;
			Solution = solution;
		}
		public int Index
		{
			get; set;
		}

		public byte[] Solution
		{
			get; set;
		}
	}
}
