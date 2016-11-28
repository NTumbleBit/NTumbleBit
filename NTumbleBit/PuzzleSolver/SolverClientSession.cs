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
		WaitingPuzzle,
		WaitingCommitments,
		WaitingFakeCommitmentsProof,
		WaitingPuzzleSolutions,
		Completed
	}

	public class PuzzleException : Exception
	{
		public PuzzleException(string message) : base(message)
		{

		}
	}

	public class SolverClientSession
	{
		public SolverClientSession(RsaPubKey serverKey)
		{
			if(serverKey == null)
				throw new ArgumentNullException("serverKey");
			_Parameters = new SolverParameters(serverKey);
		}

		public SolverClientSession(SolverParameters parameters)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
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

		public RsaPubKey ServerKey
		{
			get
			{
				return _Parameters.ServerKey;
			}
		}


		private Puzzle _Puzzle;
		public Puzzle Puzzle
		{
			get
			{
				return _Puzzle;
			}
		}


		private SolverClientStates _State = SolverClientStates.WaitingPuzzle;
		public SolverClientStates State
		{
			get
			{
				return _State;
			}
		}

		public PuzzleValue[] GeneratePuzzles(PuzzleValue puzzleValue)
		{
			if(puzzleValue == null)
				throw new ArgumentNullException("puzzleValue");
			AssertState(SolverClientStates.WaitingPuzzle);			
			var paymentPuzzle = new Puzzle(Parameters.ServerKey, puzzleValue);
			List<PuzzleSetElement> puzzles = new List<PuzzleSetElement>();
			for(int i = 0; i < Parameters.RealPuzzleCount; i++)
			{
				BlindFactor blind = null;
				Puzzle puzzle = paymentPuzzle.Blind(ref blind);
				puzzles.Add(new RealPuzzle(puzzle, blind));
			}

			for(int i = 0; i < Parameters.FakePuzzleCount; i++)
			{
				PuzzleSolution solution = null;
				Puzzle puzzle = ServerKey.GeneratePuzzle(ref solution);
				puzzles.Add(new FakePuzzle(puzzle, solution));
			}

			var puzzlesArray = puzzles.ToArray();
			NBitcoin.Utils.Shuffle(puzzlesArray, RandomUtils.GetInt32());
			PuzzleSet = new PuzzleSet(puzzlesArray);
			_State = SolverClientStates.WaitingCommitments;
			_Puzzle = paymentPuzzle;
			return PuzzleSet.PuzzleValues.ToArray();
		}


		public ClientRevelation Reveal(ServerCommitment[] commitments)
		{
			if(commitments == null)
				throw new ArgumentNullException("commitments");
			if(commitments.Length != Parameters.GetTotalCount())
				throw new ArgumentException("Expecting " + Parameters.GetTotalCount() + " commitments");
			AssertState(SolverClientStates.WaitingCommitments);

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

			PuzzleCommiments = commitments;
			_State = SolverClientStates.WaitingFakeCommitmentsProof;
			return new ClientRevelation(indexes.ToArray(), solutions.ToArray());
		}

		public BlindFactor[] GetBlindFactors(SolutionKey[] keys)
		{
			if(keys == null)
				throw new ArgumentNullException("keys");
			if(keys.Length != Parameters.FakePuzzleCount)
				throw new ArgumentException("Expecting " + Parameters.FakePuzzleCount + " keys");
			AssertState(SolverClientStates.WaitingFakeCommitmentsProof);

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

			_State = SolverClientStates.WaitingPuzzleSolutions;
			return PuzzleSet.PuzzleElements.OfType<RealPuzzle>()
				.Select(p => p.BlindFactor)
				.ToArray();
		}

		public Script CreateOfferScript(PaymentCashoutContext escrowContext)
		{
			if(escrowContext == null)
				throw new ArgumentNullException("escrowContext");
			AssertState(SolverClientStates.WaitingPuzzleSolutions);
			List<uint160> hashes = new List<uint160>();
			for(int i = 0; i < PuzzleCommiments.Length; i++)
			{
				var puzzle = PuzzleSet.PuzzleElements[i] as RealPuzzle;
				if(puzzle != null)
				{
					var commitment = PuzzleCommiments[i];
					hashes.Add(commitment.KeyHash);
				}
			}
			return escrowContext.CreateOfferScript(hashes.ToArray());
		}

		public PuzzleSolution GetSolution(Transaction cashout)
		{
			if(cashout == null)
				throw new ArgumentNullException("cashout");
			AssertState(SolverClientStates.WaitingPuzzleSolutions);
			foreach(var input in cashout.Inputs)
			{
				var solutions = SolverScriptBuilder.ExtractSolutions(input.ScriptSig, Parameters.RealPuzzleCount);
				try
				{
					return GetSolution(solutions);
				}
				catch(PuzzleException)
				{
	
				}
			}
			throw new PuzzleException("Impossible to find solution to the puzzle");
		}

		public PuzzleSolution GetSolution(Script scriptSig)
		{
			if(scriptSig == null)
				throw new ArgumentNullException("scriptSig");
			AssertState(SolverClientStates.WaitingPuzzleSolutions);
			var solutions = SolverScriptBuilder.ExtractSolutions(scriptSig, Parameters.RealPuzzleCount);
			if(solutions == null)
				throw new PuzzleException("Impossible to find solution to the puzzle");
			return GetSolution(solutions);
		}

		public PuzzleSolution GetSolution(SolutionKey[] keys)
		{
			if(keys == null)
				throw new ArgumentNullException("keys");
			if(keys.Length != Parameters.RealPuzzleCount)
				throw new ArgumentException("Expecting " + Parameters.RealPuzzleCount + " keys");
			AssertState(SolverClientStates.WaitingPuzzleSolutions);
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
