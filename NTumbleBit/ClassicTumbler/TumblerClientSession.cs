using NBitcoin;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
	public enum TumblerClientSessionStates
	{
		WaitingVoucher,
		WaitingGenerateClientTransactionKeys,
		WaitingTumblerClientTransactionKey,
		WaitingClientTransaction,
		WaitingSolvedVoucher,
		WaitingGenerateTumblerTransactionKey,
		WaitingTumblerEscrow,
		PromisePhase,
	}
	public class TumblerClientSession
	{
		public class State
		{
			public State()
			{
				KnownKeys = new List<Key>();
			}

			public TumblerClientSessionStates Status
			{
				get; set;
			}

			public int CycleStart
			{
				get; set;
			}

			public PuzzleSolution SignedVoucher
			{
				get; set;
			}
			public PuzzleValue UnsignedVoucher
			{
				get; set;
			}
			public PuzzleValue BlindedVoucher
			{
				get; set;
			}
			public BlindFactor BlindedVoucherFactor
			{
				get; set;
			}
			public EscrowInformation ClientEscrowInformation
			{
				get; set;
			}

			public EscrowInformation TumblerEscrowInformation
			{
				get; set;
			}
			public ScriptCoin TumblerEscrowedCoin
			{
				get;
				set;
			}
			public ScriptCoin ClientEscrowedCoin
			{
				get;
				set;
			}

			public PromiseClientSession.InternalState PromiseClientSessionState
			{
				get; set;
			}
			public SolverClientSession.InternalState SolverClientSessionState
			{
				get;
				set;
			}

			public List<Key> KnownKeys
			{
				get; set;
			}
			
		}
		public TumblerClientSession(ClassicTumblerParameters parameters, int cycleStart)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			InternalState = new State();
			Parameters = parameters;
			InternalState.CycleStart = cycleStart;
			InitClientSessions();
		}

		private void InitClientSessions()
		{
			PromiseClientSession = new PromiseClientSession(Parameters.CreatePromiseParamaters(), InternalState.PromiseClientSessionState);

			SolverClientSession = new SolverClientSession(Parameters.CreateSolverParamaters(), InternalState.SolverClientSessionState);

			InternalState.PromiseClientSessionState = null;
			InternalState.SolverClientSessionState = null;
		}

		public TumblerClientSession(ClassicTumblerParameters parameters, State state)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			if(state == null)
				throw new ArgumentNullException("state");
			Parameters = parameters;
			InternalState = Serializer.Clone(state);
			InitClientSessions();
		}

		public PromiseClientSession PromiseClientSession
		{
			get;
			private set;
		}

		public SolverClientSession SolverClientSession
		{
			get;
			private set;
		}

		State InternalState
		{
			get; set;
		}

		public State GetInternalState()
		{
			var clone = Serializer.Clone(InternalState);
			clone.PromiseClientSessionState = PromiseClientSession.GetInternalState();
			clone.SolverClientSessionState = SolverClientSession.GetInternalState();
			return clone;
		}


		public ClassicTumblerParameters Parameters
		{
			get;
			private set;
		}


		public void ReceiveUnsignedVoucher(PuzzleValue voucher)
		{
			AssertState(TumblerClientSessionStates.WaitingVoucher);
			var puzzle = new Puzzle(Parameters.VoucherKey, voucher);
			InternalState.UnsignedVoucher = voucher;
			BlindFactor factor = null;
			InternalState.BlindedVoucher = puzzle.Blind(ref factor).PuzzleValue;
			InternalState.BlindedVoucherFactor = factor;
			InternalState.Status = TumblerClientSessionStates.WaitingGenerateClientTransactionKeys;
		}

		public ClientEscrowInformation GenerateClientTransactionKeys()
		{
			AssertState(TumblerClientSessionStates.WaitingGenerateClientTransactionKeys);
			var escrow = new Key();
			var redeem = new Key();
			InternalState.ClientEscrowInformation = new EscrowInformation();
			InternalState.ClientEscrowInformation.OurEscrowKey = escrow.PubKey;
			InternalState.ClientEscrowInformation.RedeemKey = redeem.PubKey;
			InternalState.KnownKeys.Add(escrow);
			InternalState.KnownKeys.Add(redeem);

			var blindedVoucher = InternalState.BlindedVoucher;
			InternalState.BlindedVoucher = null;
			InternalState.Status = TumblerClientSessionStates.WaitingTumblerClientTransactionKey;
			return new ClientEscrowInformation()
			{
				Cycle = InternalState.CycleStart,
				EscrowKey = escrow.PubKey,
				RedeemKey = redeem.PubKey,
				UnsignedVoucher = blindedVoucher
			};
		}

		public Script GetTumblerChannelId()
		{
			AssertState(TumblerClientSessionStates.PromisePhase);
			return InternalState.TumblerEscrowedCoin.ScriptPubKey;
		}

		public Script GetClientChannelId()
		{
			AssertState(TumblerClientSessionStates.PromisePhase);
			return InternalState.ClientEscrowedCoin.ScriptPubKey;
		}

		public void ReceiveTumblerEscrowKey(PubKey tumblerKey)
		{
			AssertState(TumblerClientSessionStates.WaitingTumblerClientTransactionKey);
			InternalState.ClientEscrowInformation.OtherEscrowKey = tumblerKey;
			InternalState.Status = TumblerClientSessionStates.WaitingClientTransaction;
		}		

		public TxOut BuildClientEscrowTxOut()
		{
			AssertState(TumblerClientSessionStates.WaitingClientTransaction);
			var escrow = CreateClientEscrowScript();
			return new TxOut(Parameters.Denomination + Parameters.Fee, escrow.Hash);
		}
		
		private Script CreateClientEscrowScript()
		{
			return InternalState.ClientEscrowInformation.CreateEscrow(GetCycle().GetClientLockTime());
		}
		private Script CreateTumblerEscrowScript()
		{
			return InternalState.TumblerEscrowInformation.CreateEscrow(GetCycle().GetTumblerLockTime());
		}

		public TxOut BuildTumblerEscrowTxOut()
		{
			AssertState(TumblerClientSessionStates.WaitingTumblerEscrow);
			var escrow = CreateTumblerEscrowScript();
			return new TxOut(Parameters.Denomination, escrow.Hash);
		}

		public void SetClientSignedTransaction(Transaction transaction)
		{
			AssertState(TumblerClientSessionStates.WaitingClientTransaction);
			var expectedTxout = BuildClientEscrowTxOut();
			var output = transaction.Outputs.AsIndexedOutputs().Single(o => o.TxOut.ScriptPubKey == expectedTxout.ScriptPubKey && o.TxOut.Value == expectedTxout.Value);
			InternalState.ClientEscrowedCoin = new Coin(output).ToScriptCoin(CreateClientEscrowScript());
			InternalState.ClientEscrowInformation = null;
			InternalState.Status = TumblerClientSessionStates.WaitingSolvedVoucher;
		}

		public void CheckVoucherSolution(PuzzleSolution blindedVoucherSignature)
		{
			AssertState(TumblerClientSessionStates.WaitingSolvedVoucher);
			var solution = blindedVoucherSignature.Unblind(Parameters.VoucherKey, InternalState.BlindedVoucherFactor);
			if(!InternalState.UnsignedVoucher.WithRsaKey(Parameters.VoucherKey).Verify(solution))
				throw new PuzzleException("Incorrect puzzle solution");
			InternalState.BlindedVoucherFactor = null;
			InternalState.SignedVoucher = solution;
			InternalState.UnsignedVoucher = null;
			InternalState.Status = TumblerClientSessionStates.WaitingGenerateTumblerTransactionKey;
		}

		public BobEscrowInformation GenerateTumblerTransactionKey()
		{
			AssertState(TumblerClientSessionStates.WaitingGenerateTumblerTransactionKey);
			var escrow = new Key();
			InternalState.KnownKeys.Add(escrow);
			InternalState.TumblerEscrowInformation = new EscrowInformation();
			InternalState.TumblerEscrowInformation.OurEscrowKey = escrow.PubKey;
			var signedVoucher = InternalState.SignedVoucher;
			InternalState.SignedVoucher = null;
			InternalState.Status = TumblerClientSessionStates.WaitingTumblerEscrow;
			return new BobEscrowInformation()
			{
				EscrowKey = escrow.PubKey,
				SignedVoucher = signedVoucher
			};
		}

		public ScriptCoin ReceiveTumblerEscrowInformation(TumblerEscrowInformation tumblerInformation)
		{
			AssertState(TumblerClientSessionStates.WaitingTumblerEscrow);
			InternalState.TumblerEscrowInformation.OtherEscrowKey = tumblerInformation.EscrowKey;
			InternalState.TumblerEscrowInformation.RedeemKey = tumblerInformation.RedeemKey;
			var escrow = BuildTumblerEscrowTxOut();
			var output = tumblerInformation.Transaction.Outputs.AsIndexedOutputs()
				.Single(o => o.TxOut.ScriptPubKey == escrow.ScriptPubKey && o.TxOut.Value == escrow.Value);
			InternalState.TumblerEscrowedCoin = new Coin(output).ToScriptCoin(CreateTumblerEscrowScript());
			InternalState.TumblerEscrowInformation = null;
			InternalState.Status = TumblerClientSessionStates.PromisePhase;
			return InternalState.TumblerEscrowedCoin;
		}

		public CycleParameters GetCycle()
		{
			return Parameters.CycleGenerator.GetCycle(InternalState.CycleStart);
		}

		private void AssertState(TumblerClientSessionStates state)
		{
			if(state != InternalState.Status)
				throw new InvalidOperationException("Invalid state, actual " + InternalState.Status + " while expected is " + state);
		}
	}
}
