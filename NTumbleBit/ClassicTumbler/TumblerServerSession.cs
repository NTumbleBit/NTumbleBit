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
			public State()
			{
				KnownKeys = new List<Key>();
			}
			public ClassicTumblerParameters Parameters
			{
				get; set;
			}
			public CycleParameters GetCycleParameters()
			{
				return Parameters.CycleGenerator.GetCycle(CycleStart);
			}
			public EscrowInformation EscrowInformation
			{
				get;
				set;
			}

			public PuzzleValue UnsignedVoucher
			{
				get; set;
			}
			public List<Key> KnownKeys
			{
				get; set;
			}
			public int CycleStart
			{
				get;
				set;
			}
		}

		public TumblerAliceServerSession(ClassicTumblerParameters parameters,
										RsaKey tumblerKey,
										RsaKey voucherKey) : base(parameters, tumblerKey, voucherKey)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			InternalState = new State();
			InternalState.Parameters = parameters;
		}

		public PubKey ReceiveAliceEscrowInformation(ClientEscrowInformation escrowInformation)
		{
			var cycle = Parameters.CycleGenerator.GetCycle(escrowInformation.Cycle);
			InternalState.CycleStart = cycle.Start;
			var tumblerKey = new Key();
			InternalState.EscrowInformation = new EscrowInformation()
			{
				OurEscrowKey = tumblerKey.PubKey,
				OtherEscrowKey = escrowInformation.EscrowKey,
				Redeem = escrowInformation.RedeemKey,
				LockTime = cycle.GetClientLockTime()
			};
			InternalState.KnownKeys.Add(tumblerKey);
			InternalState.UnsignedVoucher = escrowInformation.UnsignedVoucher;
			return tumblerKey.PubKey;
		}

		public PuzzleSolution ConfirmAliceEscrow(Transaction transaction)
		{
			var escrow = InternalState.EscrowInformation.CreateEscrow();
			var output = transaction.Outputs.FirstOrDefault(txout => txout.ScriptPubKey == escrow.Hash.ScriptPubKey);
			if(output == null)
				throw new PuzzleException("No output containing the escrowed coin");
			if(output.Value != InternalState.Parameters.Denomination + InternalState.Parameters.Fee)
				throw new PuzzleException("Incorrect amount");
			var voucher = InternalState.UnsignedVoucher;
			InternalState.UnsignedVoucher = null;
			return voucher.WithRsaKey(VoucherKey.PubKey).Solve(VoucherKey);
		}

		public CycleParameters GetCycle()
		{
			return InternalState.GetCycleParameters();
		}
	}

	public enum TumblerBobStates
	{
		WaitingVoucherRequest,
		WaitingBobEscrowInformation,
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
			public State()
			{
				KnownKeys = new List<Key>();
			}
			public List<Key> KnownKeys
			{
				get; set;
			}
			public ClassicTumblerParameters Parameters
			{
				get; set;
			}
			public int CycleStart
			{
				get;
				set;
			}

			public CycleParameters GetCycle()
			{
				return Parameters.CycleGenerator.GetCycle(CycleStart);
			}

			public TumblerBobStates Status
			{
				get;
				set;
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
			public uint160 VoucherHash
			{
				get;
				set;
			}
		}

		public TumblerBobServerSession(ClassicTumblerParameters parameters,
										RsaKey tumblerKey,
										RsaKey voucherKey,
										int cycleStart) : base(parameters, tumblerKey, voucherKey)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			InternalState = new State();
			InternalState.Parameters = parameters;
			InternalState.CycleStart = cycleStart;
		}

		public CycleParameters GetCycle()
		{
			return InternalState.GetCycle();
		}


		public PuzzleValue GenerateUnsignedVoucher(ref PuzzleSolution solution)
		{
			AssertState(TumblerBobStates.WaitingVoucherRequest);
			var puzzle = Parameters.VoucherKey.GeneratePuzzle(ref solution);
			InternalState.VoucherHash = Hashes.Hash160(solution.ToBytes());
			InternalState.Status = TumblerBobStates.WaitingBobEscrowInformation;
			return puzzle.PuzzleValue;
		}


		public TumblerEscrowInformation ReceiveBobEscrowInformation(BobEscrowInformation bobEscrowInformation)
		{
			if(bobEscrowInformation == null)
				throw new ArgumentNullException("bobKey");
			AssertState(TumblerBobStates.WaitingBobEscrowInformation);
			if(Hashes.Hash160(bobEscrowInformation.SignedVoucher.ToBytes()) != InternalState.VoucherHash)
				throw new PuzzleException("Incorrect voucher");

			var escrow = new Key();
			var redeem = new Key();
			InternalState.KnownKeys.Add(escrow);
			InternalState.KnownKeys.Add(redeem);
			InternalState.EscrowInformation = new EscrowInformation();
			InternalState.EscrowInformation.OtherEscrowKey = bobEscrowInformation.EscrowKey;
			InternalState.EscrowInformation.OurEscrowKey = escrow.PubKey;
			InternalState.EscrowInformation.Redeem = redeem.PubKey;
			InternalState.EscrowInformation.LockTime = InternalState.GetCycle().GetTumblerLockTime();
			InternalState.VoucherHash = null;

			InternalState.Status = TumblerBobStates.WaitingSignedTransaction;

			return new TumblerEscrowInformation()
			{
				RedeemKey = redeem.PubKey,
				EscrowKey = escrow.PubKey
			};
		}

		public TxOut BuildEscrowTxOut()
		{
			AssertState(TumblerBobStates.WaitingSignedTransaction);
			var escrowScript = InternalState.EscrowInformation.CreateEscrow();
			return new TxOut(Parameters.Denomination, escrowScript.Hash);
		}

		public ScriptCoin SetSignedTransaction(Transaction transaction)
		{
			AssertState(TumblerBobStates.WaitingSignedTransaction);
			var escrow = InternalState.EscrowInformation.CreateEscrow();
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
