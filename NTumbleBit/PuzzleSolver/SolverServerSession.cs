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
using NTumbleBit.ClassicTumbler;

namespace NTumbleBit.PuzzleSolver
{
	public enum SolverServerStates
	{
		WaitingEscrow,
		WaitingPuzzles,
		WaitingRevelation,
		WaitingBlindFactor,
		WaitingOffer,
		Completed
	}
	public class SolverServerSession : EscrowReceiver
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

		public new class State : EscrowReceiver.State
		{
			public SolverServerStates Status
			{
				get; set;
			}

			public SolvedPuzzle[] SolvedPuzzles
			{
				get; set;
			}
			public Key FullfillKey
			{
				get;
				set;
			}
		}
		

		public State GetInternalState()
		{
			return Serializer.Clone(InternalState);
		}


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
			InternalState = new SolverServerSession.State();
			_ServerKey = serverKey;
			_Parameters = parameters;
		}

		public SolverServerSession(RsaKey serverKey, SolverParameters parameters, State state) 
			: this(serverKey, parameters)
		{
			if(state == null)			
				return;
			InternalState = state;
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

		public SolverServerStates Status
		{
			get
			{
				return InternalState.Status;
			}
		}		

		public override void ConfigureEscrowedCoin(ScriptCoin escrowedCoin, Key escrowKey)
		{
			AssertState(SolverServerStates.WaitingEscrow);
			base.ConfigureEscrowedCoin(escrowedCoin, escrowKey);
			InternalState.Status = SolverServerStates.WaitingPuzzles;
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
			InternalState.SolvedPuzzles = solvedPuzzles.ToArray();
			InternalState.Status = SolverServerStates.WaitingRevelation;
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
				var solvedPuzzle = InternalState.SolvedPuzzles[index];
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
					realPuzzles.Add(InternalState.SolvedPuzzles[i]);
				}
			}
			InternalState.SolvedPuzzles = realPuzzles.ToArray();
			InternalState.Status = SolverServerStates.WaitingBlindFactor;
			return fakePuzzles.Select(f => f.SolutionKey).ToArray();
		}

		public PubKey CheckBlindedFactors(BlindFactor[] blindFactors)
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
				var solvedPuzzle = InternalState.SolvedPuzzles[i];
				var unblinded = new Puzzle(Parameters.ServerKey, solvedPuzzle.Puzzle).Unblind(blindFactors[i]);
				if(unblindedPuzzle == null)
					unblindedPuzzle = unblinded;
				else if(unblinded != unblindedPuzzle)
					throw new PuzzleException("Invalid blind factor");
				y++;
			}

			InternalState.FullfillKey = new Key();
			InternalState.Status = SolverServerStates.WaitingOffer;
			return InternalState.FullfillKey.PubKey;
		}

		public SolutionKey[] GetSolutionKeys()
		{
			AssertState(SolverServerStates.WaitingOffer);
			return InternalState.SolvedPuzzles.Select(s => s.SolutionKey).ToArray();
		}

		private void AssertState(SolverServerStates state)
		{
			if(state != InternalState.Status)
				throw new InvalidOperationException("Invalid state, actual " + InternalState.Status + " while expected is " + state);
		}

		public Transaction SignOfferAndCreateFullfillTransaction(Transaction offer, Script cashout, FeeRate feeRate)
		{
			if(offer == null)
				throw new ArgumentNullException("offer");
			if(feeRate == null)
				throw new ArgumentNullException("feeRate");
			if(offer.Outputs.Count != 1)
				throw new PuzzleException("invalid-offer-tx");
			AssertState(SolverServerStates.WaitingOffer);
			var escrow = EscrowScriptBuilder.ExtractEscrowScriptPubKeyParameters(InternalState.EscrowedCoin.Redeem);

			var offerScript = SolverScriptBuilder.CreateOfferScript(
				InternalState.SolvedPuzzles.Select(p => p.SolutionKey.GetHash()).ToArray(),
				InternalState.FullfillKey.PubKey,
				escrow.RedeemKey,
				escrow.LockTime
				);

			if(offer.Outputs[0].ScriptPubKey != offerScript.Hash.ScriptPubKey)
				throw new PuzzleException("invalid-offer-tx");

			TransactionBuilder builder = new TransactionBuilder();
			builder.Extensions.Add(new EscrowBuilderExtension());
			builder.AddCoins(InternalState.EscrowedCoin);
			builder.AddKeys(InternalState.EscrowKey);
			builder.SignTransactionInPlace(offer);
			if(!builder.Verify(offer))
				throw new PuzzleException("invalid-offer-tx");

			var offerCoin = offer.Outputs.AsCoins().First().ToScriptCoin(offerScript);
			Transaction fullfill = new Transaction();
			fullfill.Inputs.Add(new TxIn(offerCoin.Outpoint));
			fullfill.Outputs.Add(new TxOut(offerCoin.Amount, cashout));
			var size = fullfill.GetVirtualSize() + 72 + offerCoin.Redeem.Length + GetSolutionKeys().Length * 21;
			fullfill.Outputs[0].Value -= feeRate.GetFee(size);

			var signature = fullfill.Inputs.AsIndexedInputs().First().Sign(InternalState.FullfillKey, offerCoin, SigHash.All);
			var fullfillScript = SolverScriptBuilder.CreateFulfillScript(signature, GetSolutionKeys());
			fullfill.Inputs[0].ScriptSig = fullfillScript + Op.GetPushOp(offerCoin.Redeem.ToBytes());

			InternalState.Status = SolverServerStates.Completed;
			return fullfill;
		}

		public override LockTime GetLockTime(CycleParameters cycle)
		{
			return cycle.GetClientLockTime();
		}
	}
}
