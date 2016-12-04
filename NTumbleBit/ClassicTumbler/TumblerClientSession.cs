using NBitcoin;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
	public class TumblerClientSession
	{
		public class State
		{
			public ClassicTumblerParameters Parameters
			{
				get; set;
			}
			public CycleParameters Cycle
			{
				get;
				set;
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
			public Key AliceEscrowKey
			{
				get; set;
			}
			public Key AliceRedeemKey
			{
				get; set;
			}
			public PubKey TumblerKey
			{
				get;
				set;
			}
			public ScriptCoin EscrowedCoin
			{
				get;
				set;
			}

			public Script CreateEscrow()
			{
				return EscrowScriptBuilder.CreateEscrow(
				new[] { TumblerKey, AliceEscrowKey.PubKey },
				AliceRedeemKey.PubKey,
				Cycle.GetClientLockTime());
			}
		}
		public TumblerClientSession(ClassicTumblerParameters parameters, CycleParameters cycle)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			if(cycle == null)
				throw new ArgumentNullException("cycle");
			InternalState = new State();
			InternalState.Parameters = parameters;
			InternalState.Cycle = cycle;
		}

		State InternalState
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
			var puzzle = new Puzzle(Parameters.VoucherKey, voucher);
			InternalState.UnsignedVoucher = voucher;
			BlindFactor factor = null;
			InternalState.BlindedVoucher = puzzle.Blind(ref factor).PuzzleValue;
			InternalState.BlindedVoucherFactor = factor;
		}

		public AliceEscrowInformation SendToEscrow(TransactionBuilder transactionBuilder, PubKey tumblerKey)
		{
			InternalState.AliceEscrowKey = new Key();
			InternalState.AliceRedeemKey = new Key();
			InternalState.TumblerKey = tumblerKey;
			var escrow = InternalState.CreateEscrow();
			transactionBuilder.Send(escrow.Hash, Parameters.Denomination + Parameters.Fee);
			return new AliceEscrowInformation()
			{
				Escrow = InternalState.AliceEscrowKey.PubKey,
				Redeem = InternalState.AliceRedeemKey.PubKey,
				UnsignedVoucher = InternalState.BlindedVoucher
			};
		}

		public void SetSignedTransaction(Transaction transaction)
		{
			var escrow = InternalState.CreateEscrow();
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


	}
}
