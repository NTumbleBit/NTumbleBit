using NTumbleBit.ClassicTumbler;
using NTumbleBit.Client.Tumbler.Services;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NTumbleBit.PuzzlePromise;

namespace NTumbleBit.Client.Tumbler
{
	public class PaymentStateMachine
	{
		public PaymentStateMachine(
			ClassicTumblerParameters parameters,
			TumblerClient client,
			ExternalServices services)
		{
			Parameters = parameters;
			AliceClient = client;
			BobClient = client;
			Services = services;
		}

		public PaymentStateMachine(
			ClassicTumblerParameters parameters,
			TumblerClient client,
			ExternalServices services,
			State state)
		{
			BobClient = client;
			AliceClient = client;
			Parameters = parameters;
			Services = services;
			if(state.NegotiationClientState != null)
			{
				StartCycle = state.NegotiationClientState.CycleStart;
				ClientChannelNegotiation = new ClientChannelNegotiation(parameters, state.NegotiationClientState);
			}
			if(state.PromiseClientState != null)
				PromiseClientSession = new PromiseClientSession(parameters.CreatePromiseParamaters(), state.PromiseClientState);
			if(state.SolverClientState != null)
				SolverClientSession = new SolverClientSession(parameters.CreateSolverParamaters(), state.SolverClientState);
		}

		public ExternalServices Services
		{
			get; set;
		}
		public TumblerClient BobClient
		{
			get; set;
		}
		public TumblerClient AliceClient
		{
			get; set;
		}
		public ClassicTumblerParameters Parameters
		{
			get; set;
		}
		public int StartCycle
		{
			get; set;
		}
		public ClientChannelNegotiation ClientChannelNegotiation
		{
			get; set;
		}

		public SolverClientSession SolverClientSession
		{
			get; set;
		}
		public PromiseClientSession PromiseClientSession
		{
			get;
			private set;
		}


		public class State
		{
			public ClientChannelNegotiation.State NegotiationClientState
			{
				get;
				set;
			}
			public PromiseClientSession.State PromiseClientState
			{
				get;
				set;
			}
			public SolverClientSession.State SolverClientState
			{
				get;
				set;
			}
		}

		public State GetInternalState()
		{
			State s = new State();
			s.SolverClientState = SolverClientSession.GetInternalState();
			s.PromiseClientState = PromiseClientSession.GetInternalState();
			s.NegotiationClientState = ClientChannelNegotiation.GetInternalState();
			return s;
		}


		public void Update()
		{
			int height = Services.BlockExplorerService.GetCurrentHeight();
			CycleParameters cycle;
			CyclePhase phase;
			if(ClientChannelNegotiation == null)
			{
				cycle = Parameters.CycleGenerator.GetRegistratingCycle(height);
				phase = CyclePhase.Registration;
			}
			else
			{
				cycle = ClientChannelNegotiation.GetCycle();
				var phases = new CyclePhase[]
				{
					CyclePhase.Registration,
					CyclePhase.ClientChannelEstablishment,
					CyclePhase.TumblerChannelEstablishment,
					CyclePhase.PaymentPhase,
					CyclePhase.TumblerCashoutPhase
				};
				if(!phases.Any(p => cycle.IsInPhase(p, height)))
					return;
				phase = phases.First(p => cycle.IsInPhase(p, height));
			}


			FeeRate feeRate = null;
			switch(phase)
			{
				case CyclePhase.Registration:
					//Client asks for voucher
					var voucherResponse = BobClient.AskUnsignedVoucher();
					//Client ensures he is in the same cycle as the tumbler (would fail if one tumbler or client's chain isn't sync)
					var tumblerCycle = Parameters.CycleGenerator.GetCycle(voucherResponse.Cycle);
					Assert(tumblerCycle.Start == cycle.Start, "invalid-phase");
					//Saving the voucher for later
					StartCycle = cycle.Start;
					ClientChannelNegotiation = new ClientChannelNegotiation(Parameters, cycle.Start);
					ClientChannelNegotiation.ReceiveUnsignedVoucher(voucherResponse.UnsignedVoucher);
					break;
				case CyclePhase.ClientChannelEstablishment:
					if(ClientChannelNegotiation.Status == TumblerClientSessionStates.WaitingGenerateClientTransactionKeys)
					{
						//Client asks the public key of the Tumbler and sends its own
						var aliceEscrowInformation = ClientChannelNegotiation.GenerateClientTransactionKeys();
						var key = AliceClient.RequestTumblerEscrowKey(aliceEscrowInformation);
						ClientChannelNegotiation.ReceiveTumblerEscrowKey(key);
						//Client create the escrow
						var txout = ClientChannelNegotiation.BuildClientEscrowTxOut();
						feeRate = GetFeeRate();
						var clientEscrowTx = Services.WalletService.FundTransaction(txout, feeRate);
						SolverClientSession = ClientChannelNegotiation.SetClientSignedTransaction(clientEscrowTx);
						var redeem = SolverClientSession.CreateRedeemTransaction(feeRate, Services.WalletService.GenerateAddress().ScriptPubKey);
						Services.BroadcastService.Broadcast(clientEscrowTx);
						Services.TrustedBroadcastService.Broadcast(redeem);
					}
					else if(ClientChannelNegotiation.Status == TumblerClientSessionStates.WaitingSolvedVoucher)
					{
						var voucher = AliceClient.ClientChannelConfirmed(SolverClientSession.EscrowedCoin.Outpoint.Hash);
						ClientChannelNegotiation.CheckVoucherSolution(voucher);
					}
					break;
				case CyclePhase.TumblerChannelEstablishment:
					//Client asks the Tumbler to make a channel
					var bobEscrowInformation = ClientChannelNegotiation.GetOpenChannelRequest();
					var tumblerInformation = BobClient.OpenChannel(bobEscrowInformation);
					PromiseClientSession = ClientChannelNegotiation.ReceiveTumblerEscrowedCoin(tumblerInformation);
					//Tell to the block explorer we need to track that address (for checking if it is confirmed in payment phase)
					Services.BlockExplorerService.Track(PromiseClientSession.EscrowedCoin.ScriptPubKey);
					//Channel is done, now need to run the promise protocol to get valid puzzle
					var cashoutDestination = Services.WalletService.GenerateAddress();
					feeRate = GetFeeRate();
					var sigReq = PromiseClientSession.CreateSignatureRequest(cashoutDestination, feeRate);
					var commiments = BobClient.SignHashes(PromiseClientSession.Id, sigReq);
					var revelation = PromiseClientSession.Reveal(commiments);
					var proof = BobClient.CheckRevelation(PromiseClientSession.Id, revelation);
					var puzzle = PromiseClientSession.CheckCommitmentProof(proof);
					SolverClientSession.AcceptPuzzle(puzzle);
					break;
				case CyclePhase.PaymentPhase:
					var tx = Services.BlockExplorerService.GetTransaction(PromiseClientSession.EscrowedCoin.Outpoint.Hash);
					cycle = ClientChannelNegotiation.GetCycle();
					//Ensure the tumbler coin is confirmed before paying anything
					if(tx == null || tx.Confirmations < cycle.SafetyPeriodDuration)
						return;
					if(SolverClientSession.Status == SolverClientStates.WaitingGeneratePuzzles)
					{
						feeRate = GetFeeRate();
						var puzzles = SolverClientSession.GeneratePuzzles();
						var commmitments = AliceClient.SolvePuzzles(SolverClientSession.Id, puzzles);
						var revelation2 = SolverClientSession.Reveal(commmitments);
						var solutionKeys = AliceClient.CheckRevelation(SolverClientSession.Id, revelation2);
						var blindFactors = SolverClientSession.GetBlindFactors(solutionKeys);
						var offerInformation = AliceClient.CheckBlindFactors(SolverClientSession.Id, blindFactors);
						var offerSignature = SolverClientSession.SignOffer(offerInformation);
						var offerRedeem = SolverClientSession.CreateOfferRedeemTransaction(feeRate, Services.WalletService.GenerateAddress().ScriptPubKey);
						//May need to find solution in the fullfillment transaction
						Services.BlockExplorerService.Track(SolverClientSession.GetOfferScriptPubKey());
						Services.TrustedBroadcastService.Broadcast(offerRedeem);
						try
						{
							solutionKeys = AliceClient.FullfillOffer(SolverClientSession.Id, offerSignature);
							SolverClientSession.CheckSolutions(solutionKeys);
						}
						catch
						{
						}
					}
					break;
				case CyclePhase.ClientCashoutPhase:
					//If the tumbler is uncooperative, he published solutions on the blockchain
					if(SolverClientSession.Status == SolverClientStates.WaitingPuzzleSolutions)
					{
						var transactions = Services.BlockExplorerService.GetTransactions(SolverClientSession.GetOfferScriptPubKey());
						SolverClientSession.CheckSolutions(transactions.Select(t => t.Transaction).ToArray());
					}

					if(SolverClientSession.Status == SolverClientStates.Completed)
					{
						var tumblingSolution = SolverClientSession.GetSolution();
						var transaction = PromiseClientSession.GetSignedTransaction(tumblingSolution);
						Services.BroadcastService.Broadcast(transaction);
					}
					break;
			}
		}

		private FeeRate GetFeeRate()
		{
			return Services.FeeService.GetFeeRate();
		}

		private void Assert(bool test, string error)
		{
			if(!test)
				throw new PuzzleException(error);
		}
	}
}
