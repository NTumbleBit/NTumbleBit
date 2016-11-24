using NTumbleBit.BouncyCastle.Math;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
	interface PuzzleSetElement
	{
		Puzzle Puzzle
		{
			get;
		}
	}
	class RealPuzzle : PuzzleSetElement
	{
		public RealPuzzle(Puzzle puzzle, BlindFactor blindFactory)
		{
			Puzzle = puzzle;
			BlindFactor = blindFactory;
		}

		public Puzzle Puzzle
		{
			get; set;
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
		public Puzzle Puzzle
		{
			get; set;
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
	class PuzzleSet
	{
		public PuzzleSet(PuzzleSetElement[] puzzles)
		{
			if(puzzles == null)
				throw new ArgumentNullException("puzzles");
			PuzzleElements = puzzles;
		}

		public PuzzleSetElement[] PuzzleElements
		{
			get; set;
		}

		public IEnumerable<PuzzleValue> PuzzleValues
		{
			get
			{
				return PuzzleElements.Select(p => p.Puzzle.PuzzleValue);
			}
		}
	}
}
