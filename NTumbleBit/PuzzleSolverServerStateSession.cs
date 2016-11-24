using NBitcoin;
using NBitcoin.Crypto;
using NTumbleBit.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public enum PuzzleSolverServerStates
	{
		WaitingPuzzles,
		WaitingFakePuzzleSolutions,
		WaitingBlindFactor,
		Completed
	}
	public class PuzzleSolverServerSession : PuzzleSolver
	{
		class SolvedPuzzle
		{
			public SolvedPuzzle(Puzzle puzzle, ChachaKey key, PuzzleSolution solution)
			{
				Puzzle = puzzle;
				_Key = key;
				Solution = solution;
			}

			public Puzzle Puzzle
			{
				get; set;
			}
			ChachaKey _Key;
			public ChachaKey Reveal()
			{
				var key = _Key;
				_Key = null;
				return key;
			}
			public PuzzleSolution Solution
			{
				get; set;
			}
		}
		public PuzzleSolverServerSession(RsaKey serverKey) : base(15, 285)
		{
			if(serverKey == null)
				throw new ArgumentNullException("serverKey");
			_ServerKey = serverKey;
		}


		private readonly RsaKey _ServerKey;
		private PuzzleSolverServerStates _State = PuzzleSolverServerStates.WaitingPuzzles;
		public PuzzleSolverServerStates State
		{
			get
			{
				return _State;
			}
		}


		public RsaKey ServerKey
		{
			get
			{
				return _ServerKey;
			}
		}

		public PuzzleCommitment[] SolvePuzzles(PuzzleValue[] puzzles)
		{
			if(puzzles == null)
				throw new ArgumentNullException("puzzles");
			if(puzzles.Length != TotalPuzzleCount)
				throw new ArgumentException("Expecting " + TotalPuzzleCount + " puzzles");
			AssertState(PuzzleSolverServerStates.WaitingPuzzles);
			List<PuzzleCommitment> commitments = new List<PuzzleCommitment>();
			List<SolvedPuzzle> solvedPuzzles = new List<SolvedPuzzle>();
			foreach(var puzzle in puzzles)
			{
				var solution = puzzle.Solve(ServerKey);
				byte[] key = null;
				var encryptedSolution = Utils.ChachaEncrypt(solution.ToBytes(), ref key);
				uint160 keyHash = new uint160(Hashes.RIPEMD160(key, key.Length));
				commitments.Add(new PuzzleCommitment(keyHash, encryptedSolution));
				solvedPuzzles.Add(new SolvedPuzzle(new Puzzle(ServerKey.PubKey, puzzle), new ChachaKey(key), solution));
			}
			_SolvedPuzzles = solvedPuzzles.ToArray();
			_State = PuzzleSolverServerStates.WaitingFakePuzzleSolutions;
			return commitments.ToArray();
		}



		private SolvedPuzzle[] _SolvedPuzzles;
		private SolvedPuzzle[] _SolvedFakePuzzles;
		private SolvedPuzzle[] _SolvedRealPuzzles;

		public ChachaKey[] GetFakePuzzleKeys(FakePuzzlesRevelation revelation)
		{
			if(revelation == null)
				throw new ArgumentNullException("puzzleSolutions");
			if(revelation.Indexes.Length != FakePuzzleCount || revelation.Solutions.Length != FakePuzzleCount)
				throw new ArgumentException("Expecting " + FakePuzzleCount + " puzzle solutions");
			AssertState(PuzzleSolverServerStates.WaitingFakePuzzleSolutions);



			List<SolvedPuzzle> fakePuzzles = new List<SolvedPuzzle>();
			for(int i = 0; i < FakePuzzleCount; i++)
			{
				var index = revelation.Indexes[i];
				var solvedPuzzle = _SolvedPuzzles[index];
				if(solvedPuzzle.Solution != revelation.Solutions[i])
				{
					throw new PuzzleException("Incorrect puzzle solution");
				}
				fakePuzzles.Add(solvedPuzzle);
			}

			List<SolvedPuzzle> realPuzzles = new List<SolvedPuzzle>();
			for(int i = 0; i < TotalPuzzleCount; i++)
			{
				if(Array.IndexOf(revelation.Indexes, i) == -1)
				{
					realPuzzles.Add(_SolvedPuzzles[i]);
				}
			}
			_SolvedPuzzles = null;

			_SolvedFakePuzzles = fakePuzzles.ToArray();
			_SolvedRealPuzzles = realPuzzles.ToArray();
			_State = PuzzleSolverServerStates.WaitingBlindFactor;
			return _SolvedFakePuzzles.Select(f => f.Reveal()).ToArray();
		}

		public ChachaKey[] GetRealPuzzleKeys(BlindFactor[] blindFactors)
		{
			if(blindFactors == null)
				throw new ArgumentNullException("blindFactors");
			if(blindFactors.Length != RealPuzzleCount)
				throw new ArgumentException("Expecting " + RealPuzzleCount + " blind factors");
			AssertState(PuzzleSolverServerStates.WaitingBlindFactor);
			List<ChachaKey> keys = new List<ChachaKey>();
			Puzzle unblindedPuzzle = null;
			int y = 0;
			for(int i = 0; i < RealPuzzleCount; i++)
			{
				var solvedPuzzle = _SolvedRealPuzzles[i];
				var unblinded = solvedPuzzle.Puzzle.Unblind(blindFactors[i]);
				if(unblindedPuzzle == null)
					unblindedPuzzle = unblinded;
				else if(unblinded != unblindedPuzzle)
					throw new PuzzleException("Invalid blind factor");
				y++;
			}
			_State = PuzzleSolverServerStates.Completed;
			return _SolvedRealPuzzles.Select(s => s.Reveal()).ToArray();
		}


		private void AssertState(PuzzleSolverServerStates state)
		{
			if(state != _State)
				throw new InvalidOperationException("Invalid state, actual " + _State + " while expected is " + state);
		}
	}
}
