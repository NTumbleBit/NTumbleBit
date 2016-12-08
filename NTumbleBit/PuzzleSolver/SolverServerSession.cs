using NBitcoin;
using NBitcoin.Crypto;
using NTumbleBit.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;

namespace NTumbleBit.PuzzleSolver
{
	public enum SolverServerStates
	{
		WaitingPuzzles,
		WaitingRevelation,
		WaitingBlindFactor,
		Completed
	}
	public class SolverServerSession
	{
		public class SolvedPuzzle
		{
			public SolvedPuzzle()
			{

			}
			public SolvedPuzzle(PuzzleValue puzzle, SolutionKey key, PuzzleSolution solution)
			{
				Puzzle = puzzle;
				SolutionKey = key;
				Solution = solution;
			}

			public PuzzleValue Puzzle
			{
				get; set;
			}
			public SolutionKey SolutionKey
			{
				get; set;
			}
			public PuzzleSolution Solution
			{
				get; set;
			}
		}

		public class InternalState
		{
			public SolverServerStates State
			{
				get; set;
			}

			public SolvedPuzzle[] SolvedPuzzles
			{
				get; set;
			}
		}

		InternalState _InternalState = new InternalState();

		public InternalState GetInternalState()
		{
			return Serializer.Clone(_InternalState);
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

		public SolverServerSession(RsaKey serverKey, SolverParameters parameters, InternalState state) 
			: this(serverKey, parameters)
		{
			if(state != null)			
				_InternalState = state;
		}


		private readonly RsaKey _ServerKey;
		public RsaKey ServerKey
		{
			get
			{
				return _ServerKey;
			}
		}

		SolverParameters _Parameters;
		public SolverParameters Parameters
		{
			get
			{
				return _Parameters;
			}
		}

		public SolverServerStates State
		{
			get
			{
				return _InternalState.State;
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
				solvedPuzzles.Add(new SolvedPuzzle(puzzle, solutionKey, solution));
			}
			_InternalState.SolvedPuzzles = solvedPuzzles.ToArray();
			_InternalState.State = SolverServerStates.WaitingRevelation;
			return commitments.ToArray();
		}

		public SolutionKey[] CheckRevelation(ClientRevelation revelation)
		{
			if(revelation == null)
				throw new ArgumentNullException("puzzleSolutions");
			if(revelation.FakeIndexes.Length != Parameters.FakePuzzleCount || revelation.Solutions.Length != Parameters.FakePuzzleCount)
				throw new ArgumentException("Expecting " + Parameters.FakePuzzleCount + " puzzle solutions");
			AssertState(SolverServerStates.WaitingRevelation);



			List<SolvedPuzzle> fakePuzzles = new List<SolvedPuzzle>();
			for(int i = 0; i < Parameters.FakePuzzleCount; i++)
			{
				var index = revelation.FakeIndexes[i];
				var solvedPuzzle = _InternalState.SolvedPuzzles[index];
				if(solvedPuzzle.Solution != revelation.Solutions[i])
				{
					throw new PuzzleException("Incorrect puzzle solution");
				}
				fakePuzzles.Add(solvedPuzzle);
			}

			List<SolvedPuzzle> realPuzzles = new List<SolvedPuzzle>();
			for(int i = 0; i < Parameters.GetTotalCount(); i++)
			{
				if(Array.IndexOf(revelation.FakeIndexes, i) == -1)
				{
					realPuzzles.Add(_InternalState.SolvedPuzzles[i]);
				}
			}
			_InternalState.SolvedPuzzles = realPuzzles.ToArray();
			_InternalState.State = SolverServerStates.WaitingBlindFactor;
			return fakePuzzles.Select(f => f.SolutionKey).ToArray();
		}

		public void CheckBlindedFactors(BlindFactor[] blindFactors)
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
				var solvedPuzzle = _InternalState.SolvedPuzzles[i];
				var unblinded = new Puzzle(Parameters.ServerKey, solvedPuzzle.Puzzle).Unblind(blindFactors[i]);
				if(unblindedPuzzle == null)
					unblindedPuzzle = unblinded;
				else if(unblinded != unblindedPuzzle)
					throw new PuzzleException("Invalid blind factor");
				y++;
			}
			_InternalState.State = SolverServerStates.Completed;
		}

		public SolutionKey[] GetSolutionKeys()
		{
			AssertState(SolverServerStates.Completed);
			return _InternalState.SolvedPuzzles.Select(s => s.SolutionKey).ToArray();
		}

		public Script GetFulfillScript(PaymentCashoutContext escrowContext, TransactionSignature cashoutSignature)
		{
			if(escrowContext == null)
				throw new ArgumentNullException("escrowContext");
			if(cashoutSignature == null)
				throw new ArgumentNullException("cashoutSignature");
			return escrowContext.CreateFulfillScript(cashoutSignature, GetSolutionKeys());
		}

		private void AssertState(SolverServerStates state)
		{
			if(state != _InternalState.State)
				throw new InvalidOperationException("Invalid state, actual " + _InternalState.State + " while expected is " + state);
		}
	}
}
