using NBitcoin;
using NBitcoin.Crypto;
using NTumbleBit.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
	public enum SolverServerStates
	{
		WaitingPuzzles,
		WaitingFakePuzzleSolutions,
		WaitingBlindFactor,
		Completed
	}
	public class SolverServerSession : Solver
	{
		class SolvedPuzzle
		{
			public SolvedPuzzle(Puzzle puzzle, SolutionKey key, PuzzleSolution solution)
			{
				Puzzle = puzzle;
				_Key = key;
				Solution = solution;
			}

			public Puzzle Puzzle
			{
				get; set;
			}
			SolutionKey _Key;
			public SolutionKey Reveal()
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
		public SolverServerSession(RsaKey serverKey) : this(serverKey, null)
		{
			if(serverKey == null)
				throw new ArgumentNullException("serverKey");
			_ServerKey = serverKey;
		}

		public SolverServerSession(RsaKey serverKey, SolverParameters parameters) : 
			base(parameters ?? SolverParameters.CreateDefault(serverKey.PubKey))
		{
			if(serverKey == null)
				throw new ArgumentNullException("serverKey");
			if(serverKey.PubKey != Parameters.ServerKey)
				throw new ArgumentNullException("Private key not matching expected public key");
			_ServerKey = serverKey;
		}


		private readonly RsaKey _ServerKey;
		private SolverServerStates _State = SolverServerStates.WaitingPuzzles;
		public SolverServerStates State
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

		public ServerCommitment[] SolvePuzzles(PuzzleValue[] puzzles)
		{
			if(puzzles == null)
				throw new ArgumentNullException("puzzles");
			if(puzzles.Length != TotalPuzzleCount)
				throw new ArgumentException("Expecting " + TotalPuzzleCount + " puzzles");
			AssertState(SolverServerStates.WaitingPuzzles);
			List<ServerCommitment> commitments = new List<ServerCommitment>();
			List<SolvedPuzzle> solvedPuzzles = new List<SolvedPuzzle>();
			foreach(var puzzle in puzzles)
			{
				var solution = puzzle.Solve(ServerKey);
				byte[] key = null;
				var encryptedSolution = Utils.ChachaEncrypt(solution.ToBytes(), ref key);
				uint160 keyHash = new uint160(Hashes.RIPEMD160(key, key.Length));
				commitments.Add(new ServerCommitment(keyHash, encryptedSolution));
				solvedPuzzles.Add(new SolvedPuzzle(new Puzzle(ServerKey.PubKey, puzzle), new SolutionKey(key), solution));
			}
			_SolvedPuzzles = solvedPuzzles.ToArray();
			_State = SolverServerStates.WaitingFakePuzzleSolutions;
			return commitments.ToArray();
		}



		private SolvedPuzzle[] _SolvedPuzzles;
		private SolvedPuzzle[] _SolvedFakePuzzles;
		private SolvedPuzzle[] _SolvedRealPuzzles;

		public SolutionKey[] GetFakePuzzleKeys(ClientRevelation revelation)
		{
			if(revelation == null)
				throw new ArgumentNullException("puzzleSolutions");
			if(revelation.Indexes.Length != FakePuzzleCount || revelation.Solutions.Length != FakePuzzleCount)
				throw new ArgumentException("Expecting " + FakePuzzleCount + " puzzle solutions");
			AssertState(SolverServerStates.WaitingFakePuzzleSolutions);



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
			_State = SolverServerStates.WaitingBlindFactor;
			return _SolvedFakePuzzles.Select(f => f.Reveal()).ToArray();
		}

		public SolutionKey[] GetRealPuzzleKeys(BlindFactor[] blindFactors)
		{
			if(blindFactors == null)
				throw new ArgumentNullException("blindFactors");
			if(blindFactors.Length != RealPuzzleCount)
				throw new ArgumentException("Expecting " + RealPuzzleCount + " blind factors");
			AssertState(SolverServerStates.WaitingBlindFactor);
			List<SolutionKey> keys = new List<SolutionKey>();
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
			_State = SolverServerStates.Completed;
			return _SolvedRealPuzzles.Select(s => s.Reveal()).ToArray();
		}


		private void AssertState(SolverServerStates state)
		{
			if(state != _State)
				throw new InvalidOperationException("Invalid state, actual " + _State + " while expected is " + state);
		}
	}
}
