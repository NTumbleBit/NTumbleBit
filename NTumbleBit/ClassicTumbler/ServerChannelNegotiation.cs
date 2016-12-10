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
	public class ServerChannelNegotiation
	{

		public ServerChannelNegotiation(ClassicTumblerParameters parameters, RsaKey tumblerKey, RsaKey voucherKey)
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

	public enum AliceServerChannelNegotiationStates
	{
		WaitingClientEscrowInformation,
		WaitingClientEscrow,
		Completed
	}

	public class AliceServerChannelNegotiation : ServerChannelNegotiation
	{
		State InternalState
		{
			get; set;
		}

		public class State
		{
			public State()
			{
			}
					
			public PuzzleValue UnsignedVoucher
			{
				get; set;
			}
			public Key EscrowKey
			{
				get; set;
			}
			public PubKey OtherEscrowKey
			{
				get; set;
			}
			public PubKey RedeemKey
			{
				get; set;
			}
			public int CycleStart
			{
				get;
				set;
			}
			public AliceServerChannelNegotiationStates Status
			{
				get;
				set;
			}
		}

		public State GetInternalState()
		{
			var state =  Serializer.Clone(InternalState);
			return state;
		}

		public AliceServerChannelNegotiation(ClassicTumblerParameters parameters,
										RsaKey tumblerKey,
										RsaKey voucherKey) : base(parameters, tumblerKey, voucherKey)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			InternalState = new State();
		}

		public AliceServerChannelNegotiation(ClassicTumblerParameters parameters,
										RsaKey tumblerKey,
										RsaKey voucherKey,
										State state) : base(parameters, tumblerKey, voucherKey)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			InternalState = Serializer.Clone(state);
		}

		public PubKey ReceiveClientEscrowInformation(ClientEscrowInformation escrowInformation)
		{
			AssertState(AliceServerChannelNegotiationStates.WaitingClientEscrowInformation);
			var cycle = Parameters.CycleGenerator.GetCycle(escrowInformation.Cycle);
			InternalState.CycleStart = cycle.Start;
			InternalState.EscrowKey = new Key();
			InternalState.OtherEscrowKey = escrowInformation.EscrowKey;
			InternalState.RedeemKey = escrowInformation.RedeemKey;
			InternalState.UnsignedVoucher = escrowInformation.UnsignedVoucher;
			InternalState.Status = AliceServerChannelNegotiationStates.WaitingClientEscrow;
			return InternalState.EscrowKey.PubKey;
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
			return EscrowScriptBuilder.CreateEscrow(new[] { InternalState.EscrowKey.PubKey, InternalState.OtherEscrowKey }, InternalState.RedeemKey, GetCycle().GetClientLockTime());
		}

		public SolverServerSession ConfirmClientEscrow(Transaction transaction, out PuzzleSolution solvedVoucher)
		{
			AssertState(AliceServerChannelNegotiationStates.WaitingClientEscrow);
			solvedVoucher = null;
			var escrow = CreateEscrowScript();
			var coin = transaction.Outputs.AsCoins().FirstOrDefault(txout => txout.ScriptPubKey == escrow.Hash.ScriptPubKey);
			if(coin == null)
				throw new PuzzleException("No output containing the escrowed coin");
			if(coin.Amount != Parameters.Denomination + Parameters.Fee)
				throw new PuzzleException("Incorrect amount");
			var voucher = InternalState.UnsignedVoucher;			
			var escrowedCoin = coin.ToScriptCoin(escrow);

			var session = new SolverServerSession(this.TumblerKey, this.Parameters.CreateSolverParamaters());				
			session.ConfigureEscrowedCoin(escrowedCoin, InternalState.EscrowKey);
			InternalState.UnsignedVoucher = null;
			InternalState.OtherEscrowKey = null;
			InternalState.RedeemKey = null;
			InternalState.EscrowKey = null;
			solvedVoucher = voucher.WithRsaKey(VoucherKey.PubKey).Solve(VoucherKey);
			InternalState.Status = AliceServerChannelNegotiationStates.Completed;
			return session;
		}		

		public CycleParameters GetCycle()
		{
			return Parameters.CycleGenerator.GetCycle(InternalState.CycleStart);
		}

		public AliceServerChannelNegotiationStates Status
		{
			get
			{
				return InternalState.Status;
			}
		}

		private void AssertState(AliceServerChannelNegotiationStates state)
		{
			if(state != Status)
				throw new InvalidOperationException("Invalid state, actual " + InternalState.Status + " while expected is " + state);
		}
	}

	public enum BobServerChannelNegotiationStates
	{
		WaitingVoucherRequest,
		WaitingBobEscrowInformation,
		WaitingSignedTransaction,
		Completed
	}
	public class BobServerChannelNegotiation : ServerChannelNegotiation
	{
		State InternalState
		{
			get; set;
		}

		public class State
		{
			public State()
			{
			}
			public Key RedeemKey
			{
				get; set;
			}
			public Key EscrowKey
			{
				get; set;
			}
			public int CycleStart
			{
				get;
				set;
			}			

			public BobServerChannelNegotiationStates Status
			{
				get;
				set;
			}			
			public uint160 VoucherHash
			{
				get;
				set;
			}
			public PubKey OtherEscrowKey
			{
				get;
				set;
			}
		}

		public CycleParameters GetCycle()
		{
			return Parameters.CycleGenerator.GetCycle(InternalState.CycleStart);
		}

		public BobServerChannelNegotiation(ClassicTumblerParameters parameters,
										RsaKey tumblerKey,
										RsaKey voucherKey,
										int cycleStart) : base(parameters, tumblerKey, voucherKey)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			InternalState = new State();
			InternalState.CycleStart = cycleStart;
		}

		public BobServerChannelNegotiation(ClassicTumblerParameters parameters,
										RsaKey tumblerKey,
										RsaKey voucherKey,
										State state) : base(parameters, tumblerKey, voucherKey)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			InternalState = Serializer.Clone(state);
		}
		
		public State GetInternalState()
		{
			var state = Serializer.Clone(InternalState);
			return state;
		}
		
		public PuzzleValue GenerateUnsignedVoucher(ref PuzzleSolution solution)
		{
			AssertState(BobServerChannelNegotiationStates.WaitingVoucherRequest);
			var puzzle = Parameters.VoucherKey.GeneratePuzzle(ref solution);
			InternalState.VoucherHash = Hashes.Hash160(solution.ToBytes());
			InternalState.Status = BobServerChannelNegotiationStates.WaitingBobEscrowInformation;
			return puzzle.PuzzleValue;
		}		

		public void ReceiveBobEscrowInformation(OpenChannelRequest openChannelRequest)
		{
			if(openChannelRequest == null)
				throw new ArgumentNullException("bobKey");
			AssertState(BobServerChannelNegotiationStates.WaitingBobEscrowInformation);
			if(openChannelRequest.SignedVoucher != InternalState.VoucherHash)
				throw new PuzzleException("Incorrect voucher");

			var escrow = new Key();
			var redeem = new Key();
			InternalState.EscrowKey = escrow;
			InternalState.OtherEscrowKey = openChannelRequest.EscrowKey;
			InternalState.RedeemKey = redeem;
			InternalState.VoucherHash = null;
			InternalState.Status = BobServerChannelNegotiationStates.WaitingSignedTransaction;
		}

		public TxOut BuildEscrowTxOut()
		{
			AssertState(BobServerChannelNegotiationStates.WaitingSignedTransaction);
			var escrowScript = CreateEscrowScript();
			return new TxOut(Parameters.Denomination, escrowScript.Hash);
		}

		private Script CreateEscrowScript()
		{
			return EscrowScriptBuilder.CreateEscrow(
				new[]
				{
					InternalState.EscrowKey.PubKey,
					InternalState.OtherEscrowKey
				},
				InternalState.RedeemKey.PubKey,
				GetCycle().GetTumblerLockTime());
		}

		public PromiseServerSession SetSignedTransaction(Transaction transaction)
		{
			AssertState(BobServerChannelNegotiationStates.WaitingSignedTransaction);
			var escrow = BuildEscrowTxOut();
			var output = transaction.Outputs.AsIndexedOutputs()
				.Single(o => o.TxOut.ScriptPubKey == escrow.ScriptPubKey && o.TxOut.Value == escrow.Value);
			var escrowedCoin = new Coin(output).ToScriptCoin(CreateEscrowScript());			
			PromiseServerSession session = new PromiseServerSession(Parameters.CreatePromiseParamaters());
			session.ConfigureEscrowedCoin(escrowedCoin, InternalState.EscrowKey, InternalState.RedeemKey);
			InternalState.EscrowKey = null;
			InternalState.RedeemKey = null;
			InternalState.Status = BobServerChannelNegotiationStates.Completed;			
			return session;
		}

		private void AssertState(BobServerChannelNegotiationStates state)
		{
			if(state != InternalState.Status)
				throw new InvalidOperationException("Invalid state, actual " + InternalState.Status + " while expected is " + state);
		}
	}
}
