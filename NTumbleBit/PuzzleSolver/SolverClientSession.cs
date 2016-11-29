using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using System;
using System.Collections.Generic;
using System.IO;
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
		private Puzzle _Puzzle;
		private SolverClientStates _State = SolverClientStates.WaitingPuzzle;
		private PuzzleSetElement[] _PuzzleElements = new PuzzleSetElement[0];


		public static SolverClientSession ReadFrom(byte[] bytes)
		{
			if(bytes == null)
				throw new ArgumentNullException("bytes");
			var ms = new MemoryStream(bytes);
			return ReadFrom(ms);
		}
		public static SolverClientSession ReadFrom(Stream stream)
		{
			if(stream == null)
				throw new ArgumentNullException("stream");
			var seria = new SolverSerializer(new SolverParameters(), stream);
			var parameters = seria.ReadParameters();
			seria = new SolverSerializer(parameters, stream);
			var client = new SolverClientSession(parameters);
			client._Puzzle = new Puzzle(parameters.ServerKey, seria.ReadPuzzle());
			client._State = (SolverClientStates)seria.ReadUInt();
			int length = (int)seria.ReadUInt();
			client._PuzzleElements = new PuzzleSetElement[length];
			for(int i = 0; i < length; i++)
			{
				var isFake = seria.Inner.ReadByte() == 0;
				var index = (int)seria.ReadUInt();
				var puzzle = new Puzzle(parameters.ServerKey, seria.ReadPuzzle());
				ServerCommitment commitment = null;
				if(seria.Inner.ReadByte() != 0)
				{
					commitment = seria.ReadCommitment();
				}
				if(isFake)
				{
					var solution = seria.ReadPuzzleSolution();
					client._PuzzleElements[i] = new FakePuzzle(puzzle, solution);
				}
				else
				{
					var blindFactor = seria.ReadBlindFactor();
					client._PuzzleElements[i] = new RealPuzzle(puzzle, blindFactor);
				}
				client._PuzzleElements[i].Index = index;
				client._PuzzleElements[i].Commitment = commitment;
				client._PuzzleElements[i].Puzzle = puzzle;
			}
			return client;
		}

		public void WriteTo(Stream stream)
		{
			if(stream == null)
				throw new ArgumentNullException("stream");
			var seria = new SolverSerializer(_Parameters, stream);
			seria.WriteParameters();
			seria.WritePuzzle(_Puzzle.PuzzleValue);
			seria.WriteUInt((uint)_State);
			seria.WriteUInt((uint)_PuzzleElements.Length);
			foreach(var el in _PuzzleElements)
			{
				var fake = el as FakePuzzle;
				seria.Inner.WriteByte((byte)(fake != null ? 0 : 1));
				WritePuzzleBase(seria, el);
				if(fake != null)
				{
					seria.WritePuzzleSolution(fake.Solution);
				}

				var real = el as RealPuzzle;
				if(real != null)
				{
					seria.WriteBlindFactor(real.BlindFactor);
				}
			}
		}
		public byte[] ToBytes()
		{
			MemoryStream ms = new MemoryStream();
			WriteTo(ms);
			ms.Position = 0;
			return ms.ToArrayEfficient();
		}

		private void WritePuzzleBase(SolverSerializer seria, PuzzleSetElement el)
		{
			seria.WriteUInt(el.Index);
			seria.WritePuzzle(el.Puzzle.PuzzleValue);
			if(el.Commitment == null)
				seria.Inner.WriteByte(0);
			else
			{
				seria.Inner.WriteByte(1);
				seria.WriteCommitment(el.Commitment);
			}
		}

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

		public Puzzle Puzzle
		{
			get
			{
				return _Puzzle;
			}
		}

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

			_PuzzleElements = puzzles.ToArray();
			NBitcoin.Utils.Shuffle(_PuzzleElements, RandomUtils.GetInt32());
			for(int i = 0; i < _PuzzleElements.Length; i++)
			{
				_PuzzleElements[i].Index = i;
			}
			_State = SolverClientStates.WaitingCommitments;
			_Puzzle = paymentPuzzle;
			return _PuzzleElements.Select(p => p.Puzzle.PuzzleValue).ToArray();
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

			foreach(var puzzle in _PuzzleElements.OfType<FakePuzzle>())
			{
				solutions.Add(puzzle.Solution);
				indexes.Add(puzzle.Index);
			}
			for(int i = 0; i < commitments.Length; i++)
			{
				_PuzzleElements[i].Commitment = commitments[i];
			}
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
			foreach(var puzzle in _PuzzleElements.OfType<FakePuzzle>())
			{
				var key = keys[y++].ToBytes(true);
				var hash = new uint160(Hashes.RIPEMD160(key, key.Length));
				if(hash != puzzle.Commitment.KeyHash)
				{
					throw new PuzzleException("Commitment hash invalid");
				}
				var solution = new PuzzleSolution(Utils.ChachaDecrypt(puzzle.Commitment.EncryptedSolution, key));
				if(solution != puzzle.Solution)
				{
					throw new PuzzleException("Commitment encrypted solution invalid");
				}
			}

			_State = SolverClientStates.WaitingPuzzleSolutions;
			return _PuzzleElements.OfType<RealPuzzle>()
				.Select(p => p.BlindFactor)
				.ToArray();
		}

		public Script CreateOfferScript(PaymentCashoutContext escrowContext)
		{
			if(escrowContext == null)
				throw new ArgumentNullException("escrowContext");
			AssertState(SolverClientStates.WaitingPuzzleSolutions);
			List<uint160> hashes = new List<uint160>();
			foreach(var puzzle in _PuzzleElements.OfType<RealPuzzle>())
			{
				var commitment = puzzle.Commitment;
				hashes.Add(commitment.KeyHash);
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
			foreach(var puzzle in _PuzzleElements.OfType<RealPuzzle>())
			{
				var key = keys[y++].ToBytes(true);
				var commitment = puzzle.Commitment;

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
			if(solution == null)
				throw new PuzzleException("Impossible to find solution to the puzzle");

			solution = solution.Unblind(ServerKey, solvedPuzzle.BlindFactor);
			_State = SolverClientStates.Completed;
			return solution;
		}

		private void AssertState(SolverClientStates state)
		{
			if(state != _State)
				throw new InvalidOperationException("Invalid state, actual " + _State + " while expected is " + state);
		}
	}
}
