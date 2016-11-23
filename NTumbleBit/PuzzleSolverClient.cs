using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class PuzzleSolverClient : PuzzleSolver
	{
		public PuzzleSolverClient(RsaKey key, Puzzle puzzle) : base(15, 285)
		{
			if(puzzle == null)
				throw new ArgumentNullException("puzzle");
			if(key == null)
				throw new ArgumentNullException("key");
			_RsaKey = key;
			_Puzzle = puzzle;
		}


		private readonly RsaKey _RsaKey;
		public RsaKey RsaKey
		{
			get
			{
				return _RsaKey;
			}
		}


		private readonly Puzzle _Puzzle;
		public Puzzle Puzzle
		{
			get
			{
				return _Puzzle;
			}
		}
	}
}
