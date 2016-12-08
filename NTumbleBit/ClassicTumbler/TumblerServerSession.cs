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
			public ScriptCoin EscrowedCoin
			{
				get;
				set;
			}
			public SolverServerSession.InternalState SolverServerSessionState
			{
				get;
				set;
			}
		}

		public State GetInternalState()
		{
			var state =  Serializer.Clone(InternalState);
			state.SolverServerSessionState = SolverServerSession.GetInternalState();
			return state;
		}

		public TumblerAliceServerSession(ClassicTumblerParameters parameters,
										RsaKey tumblerKey,
										RsaKey voucherKey) : base(parameters, tumblerKey, voucherKey)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			InternalState = new State();
			SolverServerSession = new SolverServerSession(tumblerKey, parameters.CreateSolverParamaters());
		}

		public TumblerAliceServerSession(ClassicTumblerParameters parameters,
										RsaKey tumblerKey,
										RsaKey voucherKey,
										State state) : base(parameters, tumblerKey, voucherKey)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			InternalState = Serializer.Clone(state);
			SolverServerSession = new SolverServerSession(tumblerKey, parameters.CreateSolverParamaters(), InternalState.SolverServerSessionState);
			InternalState.SolverServerSessionState = null;
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
				RedeemKey = escrowInformation.RedeemKey,
			};
			InternalState.KnownKeys.Add(tumblerKey);
			InternalState.UnsignedVoucher = escrowInformation.UnsignedVoucher;
			return tumblerKey.PubKey;
		}

		public TxOut BuildEscrowTxOut()
		{
			return new TxOut(Parameters.Denomination + Parameters.Fee, CreateEscrowScript().Hash);
		}

		public string GetChannelId()
		{
			return CreateEscrowScript().Hash.ScriptPubKey.ToHex();
		}

		private Script CreateEscrowScript()
		{
			if(InternalState.EscrowedCoin != null)
				return InternalState.EscrowedCoin.GetScriptCode();
			return InternalState.EscrowInformation.CreateEscrow(GetCycle().GetClientLockTime());
		}

		public PuzzleSolution ConfirmAliceEscrow(Transaction transaction)
		{
			var escrow = CreateEscrowScript();
			var coin = transaction.Outputs.AsCoins().FirstOrDefault(txout => txout.ScriptPubKey == escrow.Hash.ScriptPubKey);
			if(coin == null)
				throw new PuzzleException("No output containing the escrowed coin");
			if(coin.Amount != Parameters.Denomination + Parameters.Fee)
				throw new PuzzleException("Incorrect amount");
			var voucher = InternalState.UnsignedVoucher;
			InternalState.UnsignedVoucher = null;
			InternalState.EscrowInformation = null;
			InternalState.EscrowedCoin = coin.ToScriptCoin(escrow);
			return voucher.WithRsaKey(VoucherKey.PubKey).Solve(VoucherKey);
		}

		public PuzzleSolver.SolverServerSession SolverServerSession
		{
			get;
			internal set;
		}

		public CycleParameters GetCycle()
		{
			return Parameters.CycleGenerator.GetCycle(InternalState.CycleStart);
		}
	}

	public enum TumblerBobStates
	{
		WaitingVoucherRequest,
		WaitingBobEscrowInformation,
		WaitingSignedTransaction,
		Completed
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
			public int CycleStart
			{
				get;
				set;
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

			public PromiseServerSession.InternalState PromiseServerSessionState
			{
				get; set;
			}
		}

		public CycleParameters GetCycle()
		{
			return Parameters.CycleGenerator.GetCycle(InternalState.CycleStart);
		}

		public TumblerBobServerSession(ClassicTumblerParameters parameters,
										RsaKey tumblerKey,
										RsaKey voucherKey,
										int cycleStart) : base(parameters, tumblerKey, voucherKey)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			InternalState = new State();
			InternalState.CycleStart = cycleStart;
		}

		public TumblerBobServerSession(ClassicTumblerParameters parameters,
										RsaKey tumblerKey,
										RsaKey voucherKey,
										State state) : base(parameters, tumblerKey, voucherKey)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			InternalState = Serializer.Clone(state);
			InitializePromiseServerSession();
		}

		private void InitializePromiseServerSession()
		{
			if(InternalState.PromiseServerSessionState != null)
			{
				_PromiseServerSession = new PromiseServerSession(InternalState.PromiseServerSessionState, Parameters.CreatePromiseParamaters());
			}
			InternalState.PromiseServerSessionState = null;
		}

		public State GetInternalState()
		{
			var state = Serializer.Clone(InternalState);
			if(PromiseServerSession != null)
				state.PromiseServerSessionState = PromiseServerSession.GetInternalState();
			return state;
		}

		private PromiseServerSession _PromiseServerSession;
		public PromiseServerSession PromiseServerSession
		{
			get
			{
				return _PromiseServerSession;
			}
		}

		public PuzzleValue GenerateUnsignedVoucher(ref PuzzleSolution solution)
		{
			AssertState(TumblerBobStates.WaitingVoucherRequest);
			var puzzle = Parameters.VoucherKey.GeneratePuzzle(ref solution);
			InternalState.VoucherHash = Hashes.Hash160(solution.ToBytes());
			InternalState.Status = TumblerBobStates.WaitingBobEscrowInformation;
			return puzzle.PuzzleValue;
		}

		public string GetChannelId()
		{
			AssertState(TumblerBobStates.Completed);
			return InternalState.EscrowedCoin.ScriptPubKey.ToHex();
		}

		public void ReceiveBobEscrowInformation(BobEscrowInformation bobEscrowInformation)
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
			InternalState.EscrowInformation.RedeemKey = redeem.PubKey;
			InternalState.VoucherHash = null;
			_PromiseServerSession = new PromiseServerSession(escrow, Parameters.CreatePromiseParamaters());
			InternalState.Status = TumblerBobStates.WaitingSignedTransaction;
		}

		public TxOut BuildEscrowTxOut()
		{
			AssertState(TumblerBobStates.WaitingSignedTransaction);
			var escrowScript = CreateEscrowScript();
			return new TxOut(Parameters.Denomination, escrowScript.Hash);
		}

		private Script CreateEscrowScript()
		{
			return InternalState.EscrowInformation.CreateEscrow(GetCycle().GetTumblerLockTime());
		}

		public TumblerEscrowInformation SetSignedTransaction(Transaction transaction)
		{
			AssertState(TumblerBobStates.WaitingSignedTransaction);
			var escrow = BuildEscrowTxOut();
			var output = transaction.Outputs.AsIndexedOutputs()
				.Single(o => o.TxOut.ScriptPubKey == escrow.ScriptPubKey && o.TxOut.Value == escrow.Value);
			var result = new TumblerEscrowInformation()
			{
				EscrowKey = InternalState.EscrowInformation.OurEscrowKey,
				RedeemKey = InternalState.EscrowInformation.RedeemKey,
				Transaction = transaction
			};
			InternalState.EscrowedCoin = new Coin(output).ToScriptCoin(CreateEscrowScript());
			InternalState.EscrowInformation = null;
			InternalState.Status = TumblerBobStates.Completed;
			return result;
		}

		private void AssertState(TumblerBobStates state)
		{
			if(state != InternalState.Status)
				throw new InvalidOperationException("Invalid state, actual " + InternalState.Status + " while expected is " + state);
		}
	}
}
