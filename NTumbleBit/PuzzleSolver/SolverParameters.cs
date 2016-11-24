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

		}

		public static SolverParameters CreateDefault(RsaPubKey serverKey)
		{
			return new SolverParameters()
			{
				ServerKey = serverKey,
				FakePuzzleCount = 285,
				RealPuzzleCount = 15
			};
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

		public SolverSerializer CreateSerializer(Stream stream)
		{
			if(stream == null)
				throw new ArgumentNullException("stream");
			return new SolverSerializer(this, stream);
		}
	}
}
