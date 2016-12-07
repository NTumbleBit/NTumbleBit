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
		Completed,
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
			public ClassicTumblerParameters Parameters
			{
				get; set;
			}

			public int CycleStart
			{
				get; set;
			}

			public CycleParameters GetCycle()
			{
				return Parameters.CycleGenerator.GetCycle(CycleStart);
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
			InternalState.Parameters = parameters;
			InternalState.CycleStart = cycleStart;
		}

		public State InternalState
		{
			get; set;
		}


		public ClassicTumblerParameters Parameters
		{
			get
			{
				return InternalState.Parameters;
			}
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
			InternalState.ClientEscrowInformation.LockTime = InternalState.GetCycle().GetClientLockTime();
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

		public void ReceiveTumblerEscrowKey(PubKey tumblerKey)
		{
			AssertState(TumblerClientSessionStates.WaitingTumblerClientTransactionKey);
			InternalState.ClientEscrowInformation.OtherEscrowKey = tumblerKey;
			InternalState.Status = TumblerClientSessionStates.WaitingClientTransaction;
		}

		public TxOut BuildClientEscrowTxOut()
		{
			AssertState(TumblerClientSessionStates.WaitingClientTransaction);
			var escrow = InternalState.ClientEscrowInformation.CreateEscrow();
			return new TxOut(Parameters.Denomination + Parameters.Fee, escrow.Hash);
		}
		public TxOut BuildTumblerEscrowTxOut()
		{
			AssertState(TumblerClientSessionStates.WaitingTumblerEscrow);
			var escrow = InternalState.TumblerEscrowInformation.CreateEscrow();
			return new TxOut(Parameters.Denomination, escrow.Hash);
		}

		public void SetClientSignedTransaction(Transaction transaction)
		{
			AssertState(TumblerClientSessionStates.WaitingClientTransaction);
			var expectedTxout = BuildClientEscrowTxOut();
			var output = transaction.Outputs.AsIndexedOutputs().Single(o => o.TxOut.ScriptPubKey == expectedTxout.ScriptPubKey && o.TxOut.Value == expectedTxout.Value);
			InternalState.ClientEscrowedCoin = new Coin(output).ToScriptCoin(InternalState.ClientEscrowInformation.CreateEscrow());
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
			InternalState.TumblerEscrowInformation.LockTime = InternalState.GetCycle().GetTumblerLockTime();
			var signedVoucher = InternalState.SignedVoucher;
			InternalState.SignedVoucher = null;
			InternalState.Status = TumblerClientSessionStates.WaitingTumblerEscrow;
			return new BobEscrowInformation()
			{
				EscrowKey = escrow.PubKey,
				SignedVoucher = signedVoucher
			};
		}

		public void ReceiveTumblerTumblerKeys(TumblerEscrowInformation tumblerInformation)
		{
			AssertState(TumblerClientSessionStates.WaitingTumblerEscrow);
			InternalState.TumblerEscrowInformation.OtherEscrowKey = tumblerInformation.EscrowKey;
			InternalState.TumblerEscrowInformation.RedeemKey = tumblerInformation.RedeemKey;
			var escrow = BuildTumblerEscrowTxOut();
			var output = tumblerInformation.Transaction.Outputs.AsIndexedOutputs()
				.Single(o => o.TxOut.ScriptPubKey == escrow.ScriptPubKey && o.TxOut.Value == escrow.Value);

			InternalState.TumblerEscrowedCoin = new Coin(output).ToScriptCoin(InternalState.TumblerEscrowInformation.CreateEscrow());

			InternalState.TumblerEscrowInformation = null;
			InternalState.Status = TumblerClientSessionStates.Completed;
		}

		private void AssertState(TumblerClientSessionStates state)
		{
			if(state != InternalState.Status)
				throw new InvalidOperationException("Invalid state, actual " + InternalState.Status + " while expected is " + state);
		}
	}
}
