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
using NTumbleBit.PuzzlePromise;
using NTumbleBit.ClassicTumbler;

namespace NTumbleBit.PuzzleSolver
{
	public enum SolverClientStates
	{
		WaitingEscrow,
		WaitingPuzzle,
		WaitingGeneratePuzzles,
		WaitingCommitments,
		WaitingFakeCommitmentsProof,
		WaitingOffer,
		WaitingPuzzleSolutions,
		Completed
	}

	public class PuzzleException : Exception
	{
		public PuzzleException(string message) : base(message)
		{

		}
	}

	public class SolverClientSession : EscrowInitiator
	{
		protected new State InternalState
		{
			get
			{
				return (State)base.InternalState;
			}
			set
			{
				base.InternalState = value;
			}
		}
		public SolverClientSession(RsaPubKey serverKey)
		{
			if(serverKey == null)
				throw new ArgumentNullException(nameof(serverKey));
			_Parameters = new SolverParameters(serverKey);
			InternalState = new State();
		}


		private readonly SolverParameters _Parameters;
		private PuzzleSetElement[] _PuzzleElements;

		public SolverClientSession(SolverParameters parameters)
		{
			if(parameters == null)
				throw new ArgumentNullException(nameof(parameters));
			_Parameters = parameters;
			InternalState = new State();
		}

		public SolverClientSession(SolverParameters parameters, State state) : this(parameters)
		{
			if(state == null)
				return;
			InternalState = state;
			if(InternalState.FakeIndexes != null)
			{
				_PuzzleElements = new PuzzleSetElement[_Parameters.GetTotalCount()];
				int fakeI = 0, realI = 0;
				for(int i = 0; i < _PuzzleElements.Length; i++)
				{
					PuzzleSetElement element = null;
					var puzzle = new Puzzle(_Parameters.ServerKey, state.Puzzles[i]);

					if(InternalState.FakeIndexes.Contains(i))
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

		public State GetInternalState()
		{
			var state = Serializer.Clone(InternalState);
			if(_PuzzleElements != null)
			{
				var commitments = new ServerCommitment[_PuzzleElements.Length];
				var puzzles = new PuzzleValue[_PuzzleElements.Length];
				var fakeSolutions = new PuzzleSolution[Parameters.FakePuzzleCount];
				var blinds = new BlindFactor[Parameters.RealPuzzleCount];
				int fakeI = 0, realI = 0;
				for(int i = 0; i < _PuzzleElements.Length; i++)
				{
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

		public new class State : EscrowInitiator.State
		{
			public PuzzleValue Puzzle
			{
				get; set;
			}
			public SolverClientStates Status
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
			public PubKey FulfillKey
			{
				get;
				set;
			}
			public Money OfferTransactionFee
			{
				get;
				set;
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

		public SolverClientStates Status
		{
			get
			{
				return InternalState.Status;
			}
		}

		public override void ConfigureEscrowedCoin(ScriptCoin escrowedCoin, Key escrowKey, Key redeemKey)
		{
			AssertState(SolverClientStates.WaitingEscrow);
			base.ConfigureEscrowedCoin(escrowedCoin, escrowKey, redeemKey);
			InternalState.Status = SolverClientStates.WaitingPuzzle;
		}

		public void AcceptPuzzle(PuzzleValue puzzleValue)
		{
			if(puzzleValue == null)
				throw new ArgumentNullException(nameof(puzzleValue));
			AssertState(SolverClientStates.WaitingPuzzle);
			InternalState.Puzzle = puzzleValue;
			InternalState.Status = SolverClientStates.WaitingGeneratePuzzles;
		}

		public PuzzleValue[] GeneratePuzzles()
		{
			AssertState(SolverClientStates.WaitingGeneratePuzzles);
			List<PuzzleSetElement> puzzles = new List<PuzzleSetElement>();
			for(int i = 0; i < Parameters.RealPuzzleCount; i++)
			{
				BlindFactor blind = null;
				Puzzle puzzle = new Puzzle(ServerKey, InternalState.Puzzle).Blind(ref blind);
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
			InternalState.FakeIndexes = new int[Parameters.FakePuzzleCount];
			int fakeI = 0;
			for(int i = 0; i < _PuzzleElements.Length; i++)
			{
				_PuzzleElements[i].Index = i;
				if(_PuzzleElements[i] is FakePuzzle)
					InternalState.FakeIndexes[fakeI++] = i;
			}
			InternalState.Status = SolverClientStates.WaitingCommitments;
			return _PuzzleElements.Select(p => p.Puzzle.PuzzleValue).ToArray();
		}

		public ClientRevelation Reveal(ServerCommitment[] commitments)
		{
			if(commitments == null)
				throw new ArgumentNullException(nameof(commitments));
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
			InternalState.Status = SolverClientStates.WaitingFakeCommitmentsProof;
			return new ClientRevelation(InternalState.FakeIndexes, solutions.ToArray());
		}

		public BlindFactor[] GetBlindFactors(SolutionKey[] keys)
		{
			if(keys == null)
				throw new ArgumentNullException(nameof(keys));
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

			InternalState.Status = SolverClientStates.WaitingOffer;
			return _PuzzleElements.OfType<RealPuzzle>()
				.Select(p => p.BlindFactor)
				.ToArray();
		}

		public TransactionSignature SignOffer(OfferInformation offerInformation)
		{
			if(offerInformation == null)
				throw new ArgumentNullException(nameof(offerInformation));
			AssertState(SolverClientStates.WaitingOffer);
			InternalState.FulfillKey = offerInformation.FulfillKey;
			InternalState.OfferTransactionFee = offerInformation.Fee;
			Transaction tx = CreateUnsignedOfferTransaction();
			var signature = tx.Inputs.AsIndexedInputs().First().Sign(InternalState.EscrowKey, InternalState.EscrowedCoin, SigHash.All);
			InternalState.Status = SolverClientStates.WaitingPuzzleSolutions;
			return signature;
		}

		public TransactionSignature SignEscape()
		{
			AssertState(SolverClientStates.Completed);
			var dummy = new Transaction();
			dummy.Inputs.Add(new TxIn(InternalState.EscrowedCoin.Outpoint));
			dummy.Outputs.Add(new TxOut());
			return dummy.SignInput(InternalState.EscrowKey, InternalState.EscrowedCoin, SigHash.None | SigHash.AnyoneCanPay);
		}

		private Transaction CreateUnsignedOfferTransaction()
		{
			Script offer = CreateOfferScript();
			var coin = InternalState.EscrowedCoin;
			var tx = new Transaction();
			tx.Inputs.Add(new TxIn(coin.Outpoint));
			tx.Outputs.Add(new TxOut(coin.Amount, offer.Hash));
			tx.Outputs[0].Value -= InternalState.OfferTransactionFee;
			return tx;
		}

		public TrustedBroadcastRequest CreateOfferRedeemTransaction(FeeRate feeRate, Script redeemDestination)
		{
			var coin = CreateUnsignedOfferTransaction().Outputs.AsCoins().First().ToScriptCoin(CreateOfferScript());

			var unknownOutpoints = new OutPoint(uint256.Zero, 0);
			Transaction tx = new Transaction();
			tx.LockTime = CreateOfferScriptParameters().Expiration;
			tx.Inputs.Add(new TxIn(unknownOutpoints));
			tx.Inputs[0].Sequence = 0;
			tx.Outputs.Add(new TxOut(coin.Amount, redeemDestination));
			tx.Inputs[0].ScriptSig = new Script(OpcodeType.OP_0) + Op.GetPushOp(coin.Redeem.ToBytes());

			var vSize = tx.GetVirtualSize() + 80; // Size without signature + the signature size
			tx.Outputs[0].Value -= feeRate.GetFee(vSize);

			var redeemTransaction = new TrustedBroadcastRequest
			{
				Key = InternalState.RedeemKey,
				PreviousScriptPubKey = coin.Redeem.Hash.ScriptPubKey,
				Transaction = tx
			};
			//Strip redeem script information so we check if TrustedBroadcastRequest can sign correctly
			redeemTransaction.Transaction = redeemTransaction.ReSign(new Coin(unknownOutpoints, coin.TxOut));
			return redeemTransaction;
		}


		private Script CreateOfferScript()
		{
			return SolverScriptBuilder.CreateOfferScript(CreateOfferScriptParameters());
		}

		private OfferScriptPubKeyParameters CreateOfferScriptParameters()
		{
			return new OfferScriptPubKeyParameters
			{
				Hashes = _PuzzleElements.OfType<RealPuzzle>().Select(p => p.Commitment.KeyHash).ToArray(),
				FulfillKey = InternalState.FulfillKey,
				RedeemKey = InternalState.RedeemKey.PubKey,
				Expiration = EscrowScriptBuilder.ExtractEscrowScriptPubKeyParameters(InternalState.EscrowedCoin.Redeem).LockTime
			};
		}

		public Script GetOfferScriptPubKey()
		{
			return CreateOfferScript().Hash.ScriptPubKey;
		}

		public void CheckSolutions(Transaction[] transactions)
		{
			if(transactions == null)
				throw new ArgumentNullException(nameof(transactions));
			foreach(var tx in transactions)
			{
				try
				{
					CheckSolutions(tx);
					return;
				}
				catch(PuzzleException)
				{

				}
			}
			throw new PuzzleException("Impossible to find solution to the puzzle");
		}

		public void CheckSolutions(Transaction fulfillTx)
		{
			if(fulfillTx == null)
				throw new ArgumentNullException(nameof(fulfillTx));

			AssertState(SolverClientStates.WaitingPuzzleSolutions);
			foreach(var input in fulfillTx.Inputs)
			{
				var solutions = SolverScriptBuilder.ExtractSolutions(input.ScriptSig, Parameters.RealPuzzleCount);
				if(solutions == null)
					continue;
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
				throw new ArgumentNullException(nameof(scriptSig));
			AssertState(SolverClientStates.WaitingPuzzleSolutions);
			var solutions = SolverScriptBuilder.ExtractSolutions(scriptSig, Parameters.RealPuzzleCount);
			if(solutions == null)
				throw new PuzzleException("Impossible to find solution to the puzzle");
			CheckSolutions(solutions);
		}

		public void CheckSolutions(SolutionKey[] keys)
		{
			if(keys == null)
				throw new ArgumentNullException(nameof(keys));
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

			InternalState.PuzzleSolution = solution.Unblind(ServerKey, solvedPuzzle.BlindFactor);
			InternalState.Status = SolverClientStates.Completed;
		}

		public PuzzleSolution GetSolution()
		{
			AssertState(SolverClientStates.Completed);
			return InternalState.PuzzleSolution;
		}

		public override LockTime GetLockTime(CycleParameters cycle)
		{
			return cycle.GetClientLockTime();
		}

		private void AssertState(SolverClientStates state)
		{
			if(state != InternalState.Status)
				throw new InvalidOperationException("Invalid state, actual " + InternalState.Status + " while expected is " + state);
		}
	}
}
