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
	public class PuzzleSolverServerStateMachine : PuzzleSolver
	{
		public PuzzleSolverServerStateMachine(RsaKey serverKey) : base(15, 285)
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

		public PuzzleCommitment[] SolvePuzzles(Puzzle[] puzzles)
		{
			if(puzzles == null)
				throw new ArgumentNullException("puzzles");
			if(puzzles.Length != TotalPuzzleCount)
				throw new ArgumentException("Expecting " + TotalPuzzleCount + " puzzles");
			AssertState(PuzzleSolverServerStates.WaitingPuzzles);
			_Puzzles = puzzles.ToArray();
			List<PuzzleCommitment> commitments = new List<PuzzleCommitment>();
			List<ChachaKey> keys = new List<ChachaKey>();
			foreach(var puzzle in puzzles)
			{
				var solution = puzzle.Solve(ServerKey);
				byte[] key = null;
				var encryptedSolution = Utils.ChachaEncrypt(solution, ref key);
				uint160 keyHash = new uint160(Hashes.RIPEMD160(key, key.Length));
				commitments.Add(new PuzzleCommitment(keyHash, encryptedSolution));
				keys.Add(new ChachaKey(key));
			}
			_Keys = keys.ToArray();
			_State = PuzzleSolverServerStates.WaitingFakePuzzleSolutions;
			return commitments.ToArray();
		}


		private Puzzle[] _Puzzles;
		public Puzzle[] Puzzles
		{
			get
			{
				return _Puzzles;
			}
		}
		ChachaKey[] _Keys;
		public ChachaKey[] Keys
		{
			get
			{
				return _Keys;
			}
		}



		private int[] _FakeIndexes;
		public int[] FakeIndexes
		{
			get
			{
				return _FakeIndexes;
			}
		}

		public ChachaKey[] GetFakePuzzleKeys(PuzzleSolution[] puzzleSolutions)
		{
			if(puzzleSolutions == null)
				throw new ArgumentNullException("puzzleSolutions");
			if(puzzleSolutions.Length != FakePuzzleCount)
				throw new ArgumentException("Expecting " + FakePuzzleCount + " puzzle solutions");
			AssertState(PuzzleSolverServerStates.WaitingFakePuzzleSolutions);
			List<ChachaKey> keys = new List<ChachaKey>();
			foreach(var solution in puzzleSolutions)
			{
				var puzzle = Puzzles[solution.Index];
				var actualSolution = puzzle.Solve(ServerKey);
				if(!new BigInteger(1, actualSolution).Equals(new BigInteger(1, solution.Solution)))
				{
					throw new PuzzleException("Incorrect puzzle solution");
				}
				keys.Add(Keys[solution.Index]);
			}
			_FakeIndexes = puzzleSolutions.Select(s => s.Index).ToArray();
			_State = PuzzleSolverServerStates.WaitingBlindFactor;
			return keys.ToArray();
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
			for(int i = 0; i < Puzzles.Length; i++)
			{
				if(FakeIndexes.Contains(i))
					continue;
				var puzzle = Puzzles[i];
				keys.Add(Keys[i]);
				var unblinded = puzzle.RevertBlind(ServerKey.PubKey, blindFactors[y]);
				if(unblindedPuzzle == null)
					unblindedPuzzle = unblinded;
				else
				{
					if(unblinded != unblindedPuzzle)
					{
						throw new PuzzleException("Invalid blind factor");
					}
				}
				y++;
			}
			_State = PuzzleSolverServerStates.Completed;
			return keys.ToArray();
		}


		private void AssertState(PuzzleSolverServerStates state)
		{
			if(state != _State)
				throw new InvalidOperationException("Invalid state, actual " + _State + " while expected is " + state);
		}
	}
}
