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
		WaitingGenerateKeys,
		WaitingTumblerEscrowPubkey,
		WaitingSolvedVoucher
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
			public EscrowInformation EscrowInformation
			{
				get; set;
			}
			public ScriptCoin EscrowedCoin
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
			InternalState.Status = TumblerClientSessionStates.WaitingGenerateKeys;
		}

		public ClientEscrowInformation GenerateKeys()
		{
			AssertState(TumblerClientSessionStates.WaitingGenerateKeys);
			var escrow = new Key();
			var redeem = new Key();
			InternalState.EscrowInformation = new EscrowInformation();
			InternalState.EscrowInformation.OurEscrowKey = escrow.PubKey;
			InternalState.EscrowInformation.Redeem = redeem.PubKey;
			InternalState.EscrowInformation.LockTime = InternalState.GetCycle().GetClientLockTime();
			InternalState.KnownKeys.Add(escrow);
			InternalState.KnownKeys.Add(redeem);

			InternalState.Status = TumblerClientSessionStates.WaitingTumblerEscrowPubkey;
			return new ClientEscrowInformation()
			{
				Cycle = InternalState.CycleStart,
				EscrowKey = escrow.PubKey,
				RedeemKey = redeem.PubKey,
				UnsignedVoucher = InternalState.BlindedVoucher
			};
		}

		public void ReceiveTumblerEscrowKey(PubKey tumblerKey)
		{
			AssertState(TumblerClientSessionStates.WaitingTumblerEscrowPubkey);
			InternalState.EscrowInformation.OtherEscrowKey = tumblerKey;
			InternalState.Status = TumblerClientSessionStates.WaitingSolvedVoucher;
		}

		public TxOut BuildEscrowTxOut()
		{
			AssertState(TumblerClientSessionStates.WaitingSolvedVoucher);
			var escrow = InternalState.EscrowInformation.CreateEscrow();
			return new TxOut(Parameters.Denomination + Parameters.Fee, escrow.Hash);
		}

		public void SetSignedTransaction(Transaction transaction)
		{
			var escrow = InternalState.EscrowInformation.CreateEscrow();
			var output = transaction.Outputs.AsIndexedOutputs().Single(o => o.TxOut.ScriptPubKey == escrow.Hash.ScriptPubKey);
			InternalState.EscrowedCoin = new Coin(output).ToScriptCoin(escrow);
		}

		public PuzzleSolution GetSignedVoucher(PuzzleSolution blindedVoucherSignature)
		{
			var solution = blindedVoucherSignature.Unblind(Parameters.VoucherKey, InternalState.BlindedVoucherFactor);
			if(!InternalState.UnsignedVoucher.WithRsaKey(Parameters.VoucherKey).Verify(solution))
				throw new PuzzleException("Incorrect puzzle solution");
			InternalState.BlindedVoucherFactor = null;
			InternalState.SignedVoucher = solution;
			InternalState.UnsignedVoucher = null;
			return solution;
		}

		private void AssertState(TumblerClientSessionStates state)
		{
			if(state != InternalState.Status)
				throw new InvalidOperationException("Invalid state, actual " + InternalState.Status + " while expected is " + state);
		}
	}
}
