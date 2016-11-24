using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class PuzzleSolverParameters
	{
		public PuzzleSolverParameters()
		{

		}

		public static PuzzleSolverParameters CreateDefault(RsaPubKey serverKey)
		{
			return new PuzzleSolverParameters()
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

		public PuzzleSolverSerializer CreateSerializer(Stream stream)
		{
			if(stream == null)
				throw new ArgumentNullException("stream");
			return new PuzzleSolverSerializer(this, stream);
		}
	}
}
