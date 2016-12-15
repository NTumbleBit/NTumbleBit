using NTumbleBit.ClassicTumbler;
using NTumbleBit.Client.Tumbler.Services;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NTumbleBit.PuzzlePromise;
using Microsoft.Extensions.Logging;
using NTumbleBit.Common;

namespace NTumbleBit.Client.Tumbler
{
	public class PaymentStateMachine
	{
		public PaymentStateMachine(
			ClassicTumblerParameters parameters,
			TumblerClient client,
			ClientDestinationWallet destinationWallet,
			ExternalServices services)
		{
			Parameters = parameters;
			AliceClient = client;
			BobClient = client;
			Services = services;
			DestinationWallet = destinationWallet;
		}

		public PaymentStateMachine(
			ClassicTumblerParameters parameters,
			TumblerClient client,
			ClientDestinationWallet destinationWallet,
			ExternalServices services,
			State state) : this(parameters, client, destinationWallet, services)
		{
			if(state == null)
				return;
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
		public ClientDestinationWallet DestinationWallet
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
			if(SolverClientSession != null)
				s.SolverClientState = SolverClientSession.GetInternalState();
			if(PromiseClientSession != null)
				s.PromiseClientState = PromiseClientSession.GetInternalState();
			if(ClientChannelNegotiation != null)
				s.NegotiationClientState = ClientChannelNegotiation.GetInternalState();
			return s;
		}


		public void Update()
		{
			Update(null);
		}
		public void Update(ILogger logger)
		{
			logger = logger ?? new NullLogger();
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
					CyclePhase.TumblerCashoutPhase,
					CyclePhase.ClientCashoutPhase
				};
				if(!phases.Any(p => cycle.IsInPhase(p, height)))
					return;
				phase = phases.First(p => cycle.IsInPhase(p, height));
			}

			logger.LogInformation("Cycle " + cycle.Start + " in phase " + Enum.GetName(typeof(CyclePhase), phase) + ", ending in " + (cycle.GetPeriods().GetPeriod(phase).End - height) + " blocks");

			FeeRate feeRate = null;
			switch(phase)
			{
				case CyclePhase.Registration:
					if(ClientChannelNegotiation == null)
					{
						//Client asks for voucher
						var voucherResponse = BobClient.AskUnsignedVoucher();
						//Client ensures he is in the same cycle as the tumbler (would fail if one tumbler or client's chain isn't sync)
						var tumblerCycle = Parameters.CycleGenerator.GetCycle(voucherResponse.CycleStart);
						Assert(tumblerCycle.Start == cycle.Start, "invalid-phase");
						//Saving the voucher for later
						StartCycle = cycle.Start;
						ClientChannelNegotiation = new ClientChannelNegotiation(Parameters, cycle.Start);
						ClientChannelNegotiation.ReceiveUnsignedVoucher(voucherResponse);
						logger.LogInformation("Registration Complete");
					}
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
						if(clientEscrowTx == null)
						{
							logger.LogInformation("Not enough funds in the wallet to tumble");
							break;
						}
						SolverClientSession = ClientChannelNegotiation.SetClientSignedTransaction(clientEscrowTx);
						var redeem = SolverClientSession.CreateRedeemTransaction(feeRate, Services.WalletService.GenerateAddress().ScriptPubKey);
						Services.BlockExplorerService.Track(SolverClientSession.EscrowedCoin.ScriptPubKey);
						Services.BroadcastService.Broadcast(clientEscrowTx);
						Services.TrustedBroadcastService.Broadcast(redeem);
						logger.LogInformation("Client escrow broadcasted " + clientEscrowTx.GetHash());
						logger.LogInformation("Client escrow redeem " + redeem.Transaction.GetHash() + " will be broadcast later if tumbler unresponsive");
					}
					else if(ClientChannelNegotiation.Status == TumblerClientSessionStates.WaitingSolvedVoucher)
					{
						TransactionInformation clientTx = GetTransactionInformation(SolverClientSession.EscrowedCoin, true);
						if(clientTx != null && clientTx.Confirmations >= cycle.SafetyPeriodDuration)
						{
							var voucher = AliceClient.SignVoucher(new Models.SignVoucherRequest()
							{
								MerkleProof = clientTx.MerkleProof,
								Transaction = clientTx.Transaction
							});
							ClientChannelNegotiation.CheckVoucherSolution(voucher);
							logger.LogInformation("Voucher solution obtained");
						}
					}
					break;
				case CyclePhase.TumblerChannelEstablishment:
					if(ClientChannelNegotiation != null && ClientChannelNegotiation.Status == TumblerClientSessionStates.WaitingGenerateTumblerTransactionKey)
					{
						//Client asks the Tumbler to make a channel
						var bobEscrowInformation = ClientChannelNegotiation.GetOpenChannelRequest();
						var tumblerInformation = BobClient.OpenChannel(bobEscrowInformation);
						PromiseClientSession = ClientChannelNegotiation.ReceiveTumblerEscrowedCoin(tumblerInformation);
						//Tell to the block explorer we need to track that address (for checking if it is confirmed in payment phase)
						Services.BlockExplorerService.Track(PromiseClientSession.EscrowedCoin.ScriptPubKey);
						//Channel is done, now need to run the promise protocol to get valid puzzle
						var cashoutDestination = DestinationWallet.GetNewDestination();
						feeRate = GetFeeRate();
						var sigReq = PromiseClientSession.CreateSignatureRequest(cashoutDestination, feeRate);
						var commiments = BobClient.SignHashes(PromiseClientSession.Id, sigReq);
						var revelation = PromiseClientSession.Reveal(commiments);
						var proof = BobClient.CheckRevelation(PromiseClientSession.Id, revelation);
						var puzzle = PromiseClientSession.CheckCommitmentProof(proof);
						SolverClientSession.AcceptPuzzle(puzzle);
						logger.LogInformation("Tumbler escrow broadcasted " + PromiseClientSession.EscrowedCoin.Outpoint.Hash);
					}
					break;
				case CyclePhase.PaymentPhase:
					if(PromiseClientSession != null)
					{
						TransactionInformation tumblerTx = GetTransactionInformation(PromiseClientSession.EscrowedCoin, false);
						//Ensure the tumbler coin is confirmed before paying anything
						if(tumblerTx == null || tumblerTx.Confirmations < cycle.SafetyPeriodDuration)
						{
							if(tumblerTx == null)
								logger.LogInformation("Tumbler escrow " + tumblerTx.Transaction.GetHash() + " expecting " + cycle.SafetyPeriodDuration + " current is " + tumblerTx.Confirmations);
							else
								logger.LogInformation("Tumbler escrow not found");
							return;
						}
						if(SolverClientSession.Status == SolverClientStates.WaitingGeneratePuzzles)
						{
							logger.LogInformation("Tumbler escrow confirmed " + tumblerTx.Transaction.GetHash());
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
							logger.LogInformation("Offer redeem " + offerRedeem.Transaction.GetHash() + " locked until " + offerRedeem.Transaction.LockTime.Height);
							try
							{
								solutionKeys = AliceClient.FullfillOffer(SolverClientSession.Id, offerSignature);
								SolverClientSession.CheckSolutions(solutionKeys);
								logger.LogInformation("Solution recovered from cooperative tumbler");
							}
							catch
							{
								logger.LogWarning("Uncooperative tumbler detected, keep connection open.");
							}
							logger.LogInformation("Payment completed");
						}
					}
					break;
				case CyclePhase.ClientCashoutPhase:
					if(SolverClientSession != null)
					{
						//If the tumbler is uncooperative, he published solutions on the blockchain
						if(SolverClientSession.Status == SolverClientStates.WaitingPuzzleSolutions)
						{
							var transactions = Services.BlockExplorerService.GetTransactions(SolverClientSession.GetOfferScriptPubKey(), false);
							SolverClientSession.CheckSolutions(transactions.Select(t => t.Transaction).ToArray());
							logger.LogInformation("Solution recovered from blockchain transaction");
						}

						if(SolverClientSession.Status == SolverClientStates.Completed)
						{
							var tumblingSolution = SolverClientSession.GetSolution();
							var transaction = PromiseClientSession.GetSignedTransaction(tumblingSolution);
							Services.BroadcastService.Broadcast(transaction);
							logger.LogInformation("Client Cashout completed " + transaction.GetHash());
						}
					}
					break;
			}
		}

		private TransactionInformation GetTransactionInformation(ICoin coin, bool withProof)
		{
			var expectedTxout = coin.TxOut;
			var tx = Services.BlockExplorerService
				.GetTransactions(coin.TxOut.ScriptPubKey, withProof)
				.FirstOrDefault(t => t.Transaction.GetHash() == coin.Outpoint.Hash);
			return tx;
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
