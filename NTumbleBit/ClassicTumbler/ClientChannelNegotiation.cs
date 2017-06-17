using NBitcoin;
using NBitcoin.Crypto;
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
		WaitingTumblerClientTransactionKey,
		WaitingClientTransaction,
		WaitingSolvedVoucher,
		WaitingGenerateTumblerTransactionKey,
		WaitingTumblerEscrow,
		PromisePhase,
	}
	public class ClientChannelNegotiation
	{
		public class State
		{
			public State()
			{
			}

			public TumblerClientSessionStates Status
			{
				get; set;
			}

			public int CycleStart
			{
				get; set;
			}

			public byte[] SignedVoucher
			{
				get; set;
			}
			public UnsignedVoucherInformation UnsignedVoucher
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
			public Key TumblerEscrowKey
			{
				get;
				set;
			}
			public Key ClientEscrowKey
			{
				get;
				set;
			}
			public Key ClientRedeemKey
			{
				get;
				set;
			}
			public int TumblerEscrowKeyReference
			{
				get;
				set;
			}
		}
		public ClientChannelNegotiation(ClassicTumblerParameters parameters, int cycleStart)
		{
			if(parameters == null)
				throw new ArgumentNullException(nameof(parameters));
			InternalState = new State();
			Parameters = parameters;
			InternalState.CycleStart = cycleStart;
		}

		public ClientChannelNegotiation(ClassicTumblerParameters parameters, State state)
		{
			if(parameters == null)
				throw new ArgumentNullException(nameof(parameters));
			if(state == null)
				throw new ArgumentNullException(nameof(state));
			Parameters = parameters;
			InternalState = Serializer.Clone(state);
		}

		private State InternalState
		{
			get; set;
		}


		public TumblerClientSessionStates Status
		{
			get
			{
				return InternalState.Status;
			}
		}

		public State GetInternalState()
		{
			var clone = Serializer.Clone(InternalState);
			return clone;
		}


		public ClassicTumblerParameters Parameters
		{
			get;
			private set;
		}


		public void ReceiveUnsignedVoucher(UnsignedVoucherInformation voucher)
		{
			AssertState(TumblerClientSessionStates.WaitingVoucher);
			var puzzle = new Puzzle(Parameters.VoucherKey, voucher.Puzzle);
			InternalState.UnsignedVoucher = voucher;
			BlindFactor factor = null;
			InternalState.BlindedVoucher = puzzle.Blind(ref factor).PuzzleValue;
			InternalState.BlindedVoucherFactor = factor;
			InternalState.Status = TumblerClientSessionStates.WaitingTumblerClientTransactionKey;
		}

		public ClientEscrowInformation GenerateClientTransactionKeys()
		{
			AssertState(TumblerClientSessionStates.WaitingSolvedVoucher);
			var blindedVoucher = InternalState.BlindedVoucher;
			InternalState.BlindedVoucher = null;
			return new ClientEscrowInformation
			{
				Cycle = InternalState.CycleStart,
				EscrowKey = InternalState.ClientEscrowKey.PubKey,
				RedeemKey = InternalState.ClientRedeemKey.PubKey,
				UnsignedVoucher = blindedVoucher
			};
		}
		public void ReceiveTumblerEscrowKey(PubKey tumblerKey, int keyReference)
		{
			AssertState(TumblerClientSessionStates.WaitingTumblerClientTransactionKey);
			var escrow = new Key();
			var redeem = new Key();
			InternalState.ClientEscrowInformation = new EscrowInformation();
			InternalState.ClientEscrowInformation.OtherEscrowKey = tumblerKey;
			InternalState.ClientEscrowInformation.OurEscrowKey = escrow.PubKey;
			InternalState.ClientEscrowInformation.RedeemKey = redeem.PubKey;
			InternalState.ClientEscrowKey = escrow;
			InternalState.ClientRedeemKey = redeem;
			InternalState.TumblerEscrowKeyReference = keyReference;
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

		public SolverClientSession SetClientSignedTransaction(Transaction transaction, Script redeemDestination)
		{
			AssertState(TumblerClientSessionStates.WaitingClientTransaction);
			var expectedTxout = BuildClientEscrowTxOut();
			var output = transaction.Outputs.AsIndexedOutputs().Single(o => o.TxOut.ScriptPubKey == expectedTxout.ScriptPubKey && o.TxOut.Value == expectedTxout.Value);
			var solver = new SolverClientSession(Parameters.CreateSolverParamaters());
			solver.ConfigureEscrowedCoin(new Coin(output).ToScriptCoin(CreateClientEscrowScript()), InternalState.ClientEscrowKey, InternalState.ClientRedeemKey, redeemDestination);
			InternalState.Status = TumblerClientSessionStates.WaitingSolvedVoucher;
			return solver;
		}

		public void CheckVoucherSolution(PuzzleSolution blindedVoucherSignature)
		{
			AssertState(TumblerClientSessionStates.WaitingSolvedVoucher);
			var solution = blindedVoucherSignature.Unblind(Parameters.VoucherKey, InternalState.BlindedVoucherFactor);
			if(!InternalState.UnsignedVoucher.Puzzle.WithRsaKey(Parameters.VoucherKey).Verify(solution))
				throw new PuzzleException("Incorrect puzzle solution");
			InternalState.BlindedVoucherFactor = null;
			InternalState.SignedVoucher = new XORKey(solution).XOR(InternalState.UnsignedVoucher.EncryptedSignature);
			InternalState.UnsignedVoucher.EncryptedSignature = null;
			InternalState.ClientEscrowInformation = null;
			InternalState.ClientEscrowKey = null;
			InternalState.ClientRedeemKey = null;
			InternalState.Status = TumblerClientSessionStates.WaitingGenerateTumblerTransactionKey;
		}

		public OpenChannelRequest GetOpenChannelRequest()
		{
			AssertState(TumblerClientSessionStates.WaitingGenerateTumblerTransactionKey);
			var escrow = new Key();
			InternalState.TumblerEscrowKey = escrow;
			InternalState.Status = TumblerClientSessionStates.WaitingTumblerEscrow;
			var result = new OpenChannelRequest
			{
				EscrowKey = escrow.PubKey,
				Signature = InternalState.SignedVoucher,
				CycleStart = InternalState.UnsignedVoucher.CycleStart,
				Nonce = InternalState.UnsignedVoucher.Nonce,
			};
			InternalState.SignedVoucher = null;
			InternalState.UnsignedVoucher = null;
			return result;
		}

		public PromiseClientSession ReceiveTumblerEscrowedCoin(ScriptCoin escrowedCoin)
		{
			AssertState(TumblerClientSessionStates.WaitingTumblerEscrow);
			var escrow = EscrowScriptBuilder.ExtractEscrowScriptPubKeyParameters(escrowedCoin.Redeem);
			if(escrow == null || !escrow.EscrowKeys.Contains(InternalState.TumblerEscrowKey.PubKey))
				throw new PuzzleException("invalid-escrow");
			if(escrowedCoin.Amount != Parameters.Denomination)
				throw new PuzzleException("invalid-amount");


			InternalState.Status = TumblerClientSessionStates.PromisePhase;
			var session = new PromiseClientSession(Parameters.CreatePromiseParamaters());
			session.ConfigureEscrowedCoin(escrowedCoin, InternalState.TumblerEscrowKey);
			InternalState.TumblerEscrowKey = null;
			return session;
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
