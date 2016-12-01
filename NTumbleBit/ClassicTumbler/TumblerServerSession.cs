using NBitcoin;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
	public class TumblerServerSession
	{

		public TumblerServerSession(ClassicTumblerParameters parameters, RsaKey tumblerKey, RsaKey voucherKey)
		{
			if(tumblerKey == null)
				throw new ArgumentNullException("tumblerKey");
			if(voucherKey == null)
				throw new ArgumentNullException("voucherKey");
			if(parameters.VoucherKey != voucherKey.PubKey)
				throw new ArgumentException("Voucher key does not match");
			if(parameters.ServerKey != tumblerKey.PubKey)
				throw new ArgumentException("Tumbler key does not match");
			TumblerKey = tumblerKey;
			VoucherKey = voucherKey;
			Parameters = parameters;
		}

		public RsaKey TumblerKey
		{
			get;
			private set;
		}
		public RsaKey VoucherKey
		{
			get;
			private set;
		}

		public ClassicTumblerParameters Parameters
		{
			get; set;
		}
	}
	public class TumblerAliceServerSession : TumblerServerSession
	{
		State InternalState
		{
			get; set;
		}

		public class State
		{
			public ClassicTumblerParameters Parameters
			{
				get; set;
			}
			public int Cycle
			{
				get;
				set;
			}
			public AliceEscrowInformation EscrowInformation
			{
				get;
				internal set;
			}
		}

		public TumblerAliceServerSession(ClassicTumblerParameters parameters,
										RsaKey tumblerKey,
										RsaKey voucherKey,
										int cycle) : base(parameters, tumblerKey, voucherKey)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			InternalState = new State();
			InternalState.Parameters = parameters;
			InternalState.Cycle = cycle;
		}

		public void ReceiveAliceEscrowInformation(AliceEscrowInformation escrowInformation)
		{
			InternalState.EscrowInformation = escrowInformation;
		}

		public PuzzleSolution ConfirmAliceEscrow()
		{
			return InternalState.EscrowInformation.UnsignedVoucher.WithRsaKey(VoucherKey.PubKey).Solve(VoucherKey);
		}
	}

	public enum TumblerBobStates
	{
		WaitingVoucherRequest,
		WaitingSignedVoucher,
		WaitingBobPubKey,
		WaitingSignedTransaction
	}
	public class TumblerBobServerSession : TumblerServerSession
	{
		State InternalState
		{
			get; set;
		}

		public class State
		{
			public ClassicTumblerParameters Parameters
			{
				get; set;
			}
			public int Cycle
			{
				get;
				set;
			}
			public AliceEscrowInformation EscrowInformation
			{
				get;
				internal set;
			}
			public PuzzleSolution SignedVoucher
			{
				get;
				internal set;
			}
			public TumblerBobStates Status
			{
				get;
				set;
			}
			public PubKey BobPubKey
			{
				get;
				internal set;
			}
			public Key TumblerRedeemKey
			{
				get;
				internal set;
			}
			public Key TumblerEscrowKey
			{
				get;
				internal set;
			}
			public ScriptCoin EscrowedCoin
			{
				get;
				internal set;
			}

			public Script CreateEscrow()
			{
				return EscrowScriptBuilder.CreateEscrow(new PubKey[] { TumblerEscrowKey.PubKey, BobPubKey }, TumblerRedeemKey.PubKey, Parameters.CycleParameters.GetTumblerLockTime(Cycle));
			}
		}

		public TumblerBobServerSession(ClassicTumblerParameters parameters,
										RsaKey tumblerKey,
										RsaKey voucherKey,
										int cycle) : base(parameters, tumblerKey, voucherKey)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			InternalState = new State();
			InternalState.Parameters = parameters;
			InternalState.Cycle = cycle;
		}

		public PuzzleValue GenerateUnsignedVoucher()
		{
			AssertState(TumblerBobStates.WaitingVoucherRequest);
			PuzzleSolution solution = null;
			var puzzle = Parameters.VoucherKey.GeneratePuzzle(ref solution);
			InternalState.SignedVoucher = solution;
			InternalState.Status = TumblerBobStates.WaitingSignedVoucher;
			return puzzle.PuzzleValue;
		}

		public bool VerifyVoucher(PuzzleSolution solution)
		{
			AssertState(TumblerBobStates.WaitingSignedVoucher);
			var result = solution == InternalState.SignedVoucher;
			InternalState.SignedVoucher = null;
			InternalState.Status = TumblerBobStates.WaitingBobPubKey;
			return result;
		}

		public TumblerEscrowInformation SendToEscrow(TransactionBuilder builder, PubKey bobKey)
		{
			AssertState(TumblerBobStates.WaitingBobPubKey);
			InternalState.BobPubKey = bobKey;
			InternalState.TumblerRedeemKey = new Key();
			InternalState.TumblerEscrowKey = new Key();
			var escrow = InternalState.CreateEscrow();
			builder.Send(escrow.Hash, Parameters.Denomination);
			InternalState.Status = TumblerBobStates.WaitingSignedTransaction;
			return new TumblerEscrowInformation()
			{
				Escrow = InternalState.TumblerEscrowKey.PubKey,
				Redeem = InternalState.TumblerRedeemKey.PubKey
			};
		}

		public ScriptCoin SetSignedTransaction(Transaction transaction)
		{
			AssertState(TumblerBobStates.WaitingSignedTransaction);
			var escrow = InternalState.CreateEscrow();
			var output = transaction.Outputs.AsIndexedOutputs().Single(o => o.TxOut.ScriptPubKey == escrow.Hash.ScriptPubKey);
			InternalState.EscrowedCoin = new Coin(output).ToScriptCoin(escrow);
			return InternalState.EscrowedCoin;
		}

		private void AssertState(TumblerBobStates state)
		{
			if(state != InternalState.Status)
				throw new InvalidOperationException("Invalid state, actual " + InternalState.Status + " while expected is " + state);
		}
	}
}
