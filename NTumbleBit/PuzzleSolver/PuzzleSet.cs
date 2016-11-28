using NTumbleBit.BouncyCastle.Math;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
	internal class PuzzleSetElement
	{
		public Puzzle Puzzle
		{
			get;
			set;
		}

		public int Index
		{
			get;
			set;
		}

		public ServerCommitment Commitment
		{
			get;
			set;
		}
	}
	class RealPuzzle : PuzzleSetElement
	{
		public RealPuzzle(Puzzle puzzle, BlindFactor blindFactory)
		{
			Puzzle = puzzle;
			BlindFactor = blindFactory;
		}

		public BlindFactor BlindFactor
		{
			get; set;
		}

		public override string ToString()
		{
			return "+Real " + Puzzle.ToString();
		}
	}

	class FakePuzzle : PuzzleSetElement
	{
		public FakePuzzle(Puzzle puzzle, PuzzleSolution solution)
		{
			Puzzle = puzzle;
			Solution = solution;
		}
	
		public PuzzleSolution Solution
		{
			get;
			private set;
		}

		public override string ToString()
		{
			return "-Fake " + Puzzle.ToString();
		}
	}
}
