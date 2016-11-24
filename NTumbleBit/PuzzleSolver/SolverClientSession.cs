using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
	public enum SolverClientStates
	{
		Initialized,
		WaitingCommitments,
		WaitingEncryptedFakePuzzleKeys,
		WaitingEncryptedRealPuzzleKeys,
		Completed
	}

	public class PuzzleException : Exception
	{
		public PuzzleException(string message) : base(message)
		{

		}
	}

	public class SolverClientSession : Solver
	{
		public SolverClientSession(RsaPubKey serverKey, PuzzleValue puzzle)
			: base(SolverParameters.CreateDefault(serverKey))
		{
			if(puzzle == null)
				throw new ArgumentNullException("puzzle");
			_Puzzle = new Puzzle(serverKey, puzzle);
		}

		public SolverClientSession(PuzzleValue puzzle, SolverParameters parameters) : base(parameters)
		{
			if(puzzle == null)
				throw new ArgumentNullException("puzzle");
			_Puzzle = new Puzzle(parameters.ServerKey, puzzle);
		}

		public SolverClientSession(Puzzle puzzle) : base(SolverParameters.CreateDefault(puzzle.RsaPubKey))
		{
			if(puzzle == null)
				throw new ArgumentNullException("puzzle");
			_Puzzle = puzzle;
		}

		public RsaPubKey ServerKey
		{
			get
			{
				return _Puzzle.RsaPubKey;
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


		private SolverClientStates _State = SolverClientStates.Initialized;
		public SolverClientStates State
		{
			get
			{
				return _State;
			}
		}

		public PuzzleValue[] GeneratePuzzles()
		{
			AssertState(SolverClientStates.Initialized);
			List<PuzzleSetElement> puzzles = new List<PuzzleSetElement>();
			for(int i = 0; i < RealPuzzleCount; i++)
			{
				BlindFactor blind = null;
				Puzzle puzzle = Puzzle.Blind(ref blind);
				puzzles.Add(new RealPuzzle(puzzle, blind));
			}

			for(int i = 0; i < FakePuzzleCount; i++)
			{
				PuzzleSolution solution = null;
				Puzzle puzzle = ServerKey.GeneratePuzzle(ref solution);
				puzzles.Add(new FakePuzzle(puzzle, solution));
			}

			var puzzlesArray = puzzles.ToArray();
			NBitcoin.Utils.Shuffle(puzzlesArray, RandomUtils.GetInt32());
			PuzzleSet = new PuzzleSet(puzzlesArray);
			_State = SolverClientStates.WaitingCommitments;
			return PuzzleSet.PuzzleValues.ToArray();
		}


		public ClientRevelation Reveal(ServerCommitment[] commitments)
		{
			if(commitments == null)
				throw new ArgumentNullException("commitments");
			if(commitments.Length != TotalPuzzleCount)
				throw new ArgumentException("Expecting " + TotalPuzzleCount + " commitments");
			AssertState(SolverClientStates.WaitingCommitments);
			PuzzleCommiments = commitments;
			_State = SolverClientStates.WaitingEncryptedFakePuzzleKeys;

			List<PuzzleSolution> solutions = new List<PuzzleSolution>();
			List<int> indexes = new List<int>();

			for(int i = 0; i < PuzzleSet.PuzzleElements.Length; i++)
			{
				var element = PuzzleSet.PuzzleElements[i] as FakePuzzle;
				if(element != null)
				{
					solutions.Add(element.Solution);
					indexes.Add(i);
				}
			}
			return new ClientRevelation(indexes.ToArray(), solutions.ToArray());
		}

		public BlindFactor[] GetBlindFactors(SolutionKey[] keys)
		{
			if(keys == null)
				throw new ArgumentNullException("keys");
			if(keys.Length != FakePuzzleCount)
				throw new ArgumentException("Expecting " + FakePuzzleCount + " keys");
			AssertState(SolverClientStates.WaitingEncryptedFakePuzzleKeys);

			int y = 0;
			for(int i = 0; i < PuzzleCommiments.Length; i++)
			{
				var puzzle = PuzzleSet.PuzzleElements[i] as FakePuzzle;
				if(puzzle != null)
				{
					var key = keys[y++].ToBytes(true);
					var commitment = PuzzleCommiments[i];

					var hash = new uint160(Hashes.RIPEMD160(key, key.Length));
					if(hash != commitment.KeyHash)
					{
						throw new PuzzleException("Commitment hash invalid");
					}
					var solution = new PuzzleSolution(Utils.ChachaDecrypt(commitment.EncryptedSolution, key));
					if(solution != puzzle.Solution)
					{
						throw new PuzzleException("Commitment encrypted solution invalid");
					}
				}
			}

			_State = SolverClientStates.WaitingEncryptedRealPuzzleKeys;
			return PuzzleSet.PuzzleElements.OfType<RealPuzzle>()
				.Select(p => p.BlindFactor)
				.ToArray();
		}

		public PuzzleSolution GetSolution(SolutionKey[] keys)
		{
			if(keys == null)
				throw new ArgumentNullException("keys");
			if(keys.Length != RealPuzzleCount)
				throw new ArgumentException("Expecting " + RealPuzzleCount + " keys");
			AssertState(SolverClientStates.WaitingEncryptedRealPuzzleKeys);
			PuzzleSolution solution = null;
			RealPuzzle solvedPuzzle = null;
			int y = 0;
			for(int i = 0; i < PuzzleCommiments.Length; i++)
			{
				var puzzle = PuzzleSet.PuzzleElements[i] as RealPuzzle;
				if(puzzle != null)
				{
					var key = keys[y++].ToBytes(true);
					var commitment = PuzzleCommiments[i];

					var hash = new uint160(Hashes.RIPEMD160(key, key.Length));
					if(hash == commitment.KeyHash)
					{
						var decryptedSolution = new PuzzleSolution(Utils.ChachaDecrypt(commitment.EncryptedSolution, key));
						if(puzzle.Puzzle.Verify(decryptedSolution))
						{
							solution = decryptedSolution;
							solvedPuzzle = puzzle;
							break;
						}
					}
				}
			}
			if(solution == null)
				throw new PuzzleException("Impossible to find solution to the puzzle");

			solution = solution.Unblind(ServerKey, solvedPuzzle.BlindFactor);
			_State = SolverClientStates.Completed;
			return solution;
		}

		public ServerCommitment[] PuzzleCommiments
		{
			get;
			private set;
		}

		private void AssertState(SolverClientStates state)
		{
			if(state != _State)
				throw new InvalidOperationException("Invalid state, actual " + _State + " while expected is " + state);
		}

		PuzzleSet PuzzleSet
		{
			get;
			set;
		}
	}
}
