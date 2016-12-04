using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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


		private readonly SolverParameters _Parameters;
		private Puzzle _Puzzle;
		private SolverClientStates _State = SolverClientStates.WaitingPuzzle;
		private PuzzleSetElement[] _PuzzleElements;
		private PuzzleSolution _PuzzleSolution;
		private int[] _FakeIndexes;

		public SolverClientSession(SolverParameters parameters)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			_Parameters = parameters;
		}

		SolverClientSession(InternalState state)
		{
			if(state == null)
				throw new ArgumentNullException("state");

			_Parameters = state.SolverParameters;
			_Puzzle = new Puzzle(_Parameters.ServerKey, state.Puzzle);
			_PuzzleSolution = state.PuzzleSolution;
			_FakeIndexes = state.FakeIndexes;
			_State = state.State;
			if(_FakeIndexes != null)
			{
				_PuzzleElements = new PuzzleSetElement[_Parameters.GetTotalCount()];
				int fakeI = 0, realI = 0;
				for(int i = 0; i < _PuzzleElements.Length; i++)
				{
					PuzzleSetElement element = null;
					var puzzle = new Puzzle(_Parameters.ServerKey, state.Puzzles[i]);

					if(_FakeIndexes.Contains(i))
					{
						element = new FakePuzzle(puzzle, state.FakeSolutions[fakeI++]);
					}
					else
					{
						element = new RealPuzzle(puzzle, state.BlindFactors[realI++]);						
					}
					element.Index = i;
					element.Commitment = state.Commitments[i];
					_PuzzleElements[i] = element;
				}
			}
		}

		private InternalState GetInternalState()
		{
			InternalState state = new InternalState();
			state.SolverParameters = Parameters;
			state.Puzzle = Puzzle?.PuzzleValue;
			state.State = State;
			state.PuzzleSolution = _PuzzleSolution;
			state.FakeIndexes = _FakeIndexes;
			if(_PuzzleElements != null)
			{
				var indexes = new int[_PuzzleElements.Length];
				var commitments = new ServerCommitment[_PuzzleElements.Length];
				var puzzles = new PuzzleValue[_PuzzleElements.Length];
				var fakeSolutions = new PuzzleSolution[Parameters.FakePuzzleCount];
				var blinds = new BlindFactor[Parameters.RealPuzzleCount];
				int fakeI = 0, realI = 0;
				for(int i = 0; i < _PuzzleElements.Length; i++)
				{
					indexes[i] = _PuzzleElements[i].Index;
					commitments[i] = _PuzzleElements[i].Commitment;
					puzzles[i] = _PuzzleElements[i].Puzzle.PuzzleValue;
					var fake = _PuzzleElements[i] as FakePuzzle;
					if(fake != null)
					{
						fakeSolutions[fakeI++] = fake.Solution;
					}

					var real = _PuzzleElements[i] as RealPuzzle;
					if(real != null)
					{
						blinds[realI++] = real.BlindFactor;
					}
				}
				state.FakeSolutions = fakeSolutions;
				state.BlindFactors = blinds;
				state.Commitments = commitments;
				state.Puzzles = puzzles;
			}
			return state;
		}

		class InternalState
		{
			public SolverParameters SolverParameters
			{
				get; set;
			}
			public PuzzleValue Puzzle
			{
				get; set;
			}
			[JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
			public SolverClientStates State
			{
				get; set;
			}
			public PuzzleSolution PuzzleSolution
			{
				get; set;
			}

			public PuzzleValue[] Puzzles
			{
				get; set;
			}
			public ServerCommitment[] Commitments
			{
				get; set;
			}			
			public int[] FakeIndexes
			{
				get; set;
			}
			public PuzzleSolution[] FakeSolutions
			{
				get;
				set;
			}
			public BlindFactor[] BlindFactors
			{
				get;
				set;
			}
		}

		public static SolverClientSession ReadFrom(byte[] bytes)
		{
			if(bytes == null)
				throw new ArgumentNullException("bytes");
			var ms = new MemoryStream(bytes);
			return ReadFrom(ms);
		}
		public static SolverClientSession ReadFrom(Stream stream, RsaKey privateKey = null)
		{
			if(stream == null)
				throw new ArgumentNullException("stream");

			var text = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
			JsonSerializerSettings settings = new JsonSerializerSettings();
			Serializer.RegisterFrontConverters(settings);
			var state = JsonConvert.DeserializeObject<InternalState>(text, settings);
			return new SolverClientSession(state);
		}
		public void WriteTo(Stream stream)
		{
			if(stream == null)
				throw new ArgumentNullException("stream");
			var writer = new StreamWriter(stream, Encoding.UTF8);
			JsonSerializerSettings settings = new JsonSerializerSettings();
			Serializer.RegisterFrontConverters(settings);
			var result = JsonConvert.SerializeObject(this.GetInternalState(), settings);
			writer.Write(result);
			writer.Flush();
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
			_FakeIndexes = new int[Parameters.FakePuzzleCount];
			int fakeI = 0;
			for(int i = 0; i < _PuzzleElements.Length; i++)
			{
				_PuzzleElements[i].Index = i;
				if(_PuzzleElements[i] is FakePuzzle)
					_FakeIndexes[fakeI++] = i;
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
			return new ClientRevelation(_FakeIndexes, solutions.ToArray());
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

		public void CheckSolutions(Transaction cashout)
		{
			if(cashout == null)
				throw new ArgumentNullException("cashout");
			AssertState(SolverClientStates.WaitingPuzzleSolutions);
			foreach(var input in cashout.Inputs)
			{
				var solutions = SolverScriptBuilder.ExtractSolutions(input.ScriptSig, Parameters.RealPuzzleCount);
				try
				{
					CheckSolutions(solutions);
					return;
				}
				catch(PuzzleException)
				{

				}
			}
			throw new PuzzleException("Impossible to find solution to the puzzle");
		}

		public void CheckSolutions(Script scriptSig)
		{
			if(scriptSig == null)
				throw new ArgumentNullException("scriptSig");
			AssertState(SolverClientStates.WaitingPuzzleSolutions);
			var solutions = SolverScriptBuilder.ExtractSolutions(scriptSig, Parameters.RealPuzzleCount);
			if(solutions == null)
				throw new PuzzleException("Impossible to find solution to the puzzle");
			CheckSolutions(solutions);
		}

		public void CheckSolutions(SolutionKey[] keys)
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

			_PuzzleSolution = solution.Unblind(ServerKey, solvedPuzzle.BlindFactor);
			_State = SolverClientStates.Completed;
		}

		public PuzzleSolution GetSolution()
		{
			AssertState(SolverClientStates.Completed);
			return _PuzzleSolution;
		}

		private void AssertState(SolverClientStates state)
		{
			if(state != _State)
				throw new InvalidOperationException("Invalid state, actual " + _State + " while expected is " + state);
		}
	}
}
