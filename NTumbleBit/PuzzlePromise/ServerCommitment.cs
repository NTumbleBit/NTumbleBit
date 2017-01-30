using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
    public class ServerCommitment
    {
		public ServerCommitment(PuzzleValue puzzleValue, byte[] promise)
		{
			Puzzle = puzzleValue;
			Promise = promise;
		}
		public ServerCommitment()
		{

		}

		public PuzzleValue Puzzle
		{
			get; set;
		}
		public byte[] Promise
		{
			get; set;
		}
	}
}
