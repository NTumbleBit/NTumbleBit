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
	public class SolverServerSession
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
		}

		public SolverServerSession(RsaKey serverKey, SolverParameters parameters)
		{
			parameters = parameters ?? new SolverParameters(serverKey.PubKey);
			if(serverKey == null)
				throw new ArgumentNullException("serverKey");
			if(serverKey.PubKey != parameters.ServerKey)
				throw new ArgumentNullException("Private key not matching expected public key");
			_ServerKey = serverKey;
			_Parameters = parameters;
		}

		private readonly SolverParameters _Parameters;
		public SolverParameters Parameters
		{
			get
			{
				return _Parameters;
			}
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
			if(puzzles.Length != Parameters.GetTotalCount())
				throw new ArgumentException("Expecting " + Parameters.GetTotalCount() + " puzzles");
			AssertState(SolverServerStates.WaitingPuzzles);
			List<ServerCommitment> commitments = new List<ServerCommitment>();
			List<SolvedPuzzle> solvedPuzzles = new List<SolvedPuzzle>();
			foreach(var puzzle in puzzles)
			{
				var solution = puzzle.Solve(ServerKey);
				byte[] key = null;
				var encryptedSolution = Utils.ChachaEncrypt(solution.ToBytes(), ref key);
				var solutionKey = new SolutionKey(key);
				uint160 keyHash = solutionKey.GetHash();
				commitments.Add(new ServerCommitment(keyHash, encryptedSolution));
				solvedPuzzles.Add(new SolvedPuzzle(new Puzzle(ServerKey.PubKey, puzzle), solutionKey, solution));
			}
			_SolvedPuzzles = solvedPuzzles.ToArray();
			_State = SolverServerStates.WaitingFakePuzzleSolutions;
			return commitments.ToArray();
		}



		private SolvedPuzzle[] _SolvedPuzzles;
		private SolvedPuzzle[] _SolvedFakePuzzles;
		private SolvedPuzzle[] _SolvedRealPuzzles;

		public SolutionKey[] GetSolutionKeys(ClientRevelation revelation)
		{
			if(revelation == null)
				throw new ArgumentNullException("puzzleSolutions");
			if(revelation.Indexes.Length != Parameters.FakePuzzleCount || revelation.Solutions.Length != Parameters.FakePuzzleCount)
				throw new ArgumentException("Expecting " + Parameters.FakePuzzleCount + " puzzle solutions");
			AssertState(SolverServerStates.WaitingFakePuzzleSolutions);



			List<SolvedPuzzle> fakePuzzles = new List<SolvedPuzzle>();
			for(int i = 0; i < Parameters.FakePuzzleCount; i++)
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
			for(int i = 0; i < Parameters.GetTotalCount(); i++)
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

		public SolutionKey[] GetSolutionKeys(BlindFactor[] blindFactors)
		{
			if(blindFactors == null)
				throw new ArgumentNullException("blindFactors");
			if(blindFactors.Length != Parameters.RealPuzzleCount)
				throw new ArgumentException("Expecting " + Parameters.RealPuzzleCount + " blind factors");
			AssertState(SolverServerStates.WaitingBlindFactor);
			List<SolutionKey> keys = new List<SolutionKey>();
			Puzzle unblindedPuzzle = null;
			int y = 0;
			for(int i = 0; i < Parameters.RealPuzzleCount; i++)
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

		public Script GetSolutionKeys(BlindFactor[] blindFactors, EscrowContext escrowContext, TransactionSignature cashoutSignature)
		{
			if(escrowContext == null)
				throw new ArgumentNullException("escrowContext");
			if(cashoutSignature == null)
				throw new ArgumentNullException("cashoutSignature");
			return escrowContext.CreateEscrowCashout(escrowContext, cashoutSignature, GetSolutionKeys(blindFactors));
		}

		private void AssertState(SolverServerStates state)
		{
			if(state != _State)
				throw new InvalidOperationException("Invalid state, actual " + _State + " while expected is " + state);
		}
	}
}
