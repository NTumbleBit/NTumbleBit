using NTumbleBit.ClassicTumbler;
using NTumbleBit;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NTumbleBit.PuzzlePromise;
using Microsoft.Extensions.Logging;
using NTumbleBit.Logging;
using NTumbleBit.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NTumbleBit.ClassicTumbler.Server.Models;

namespace NTumbleBit.ClassicTumbler.Client
{
	public class PaymentStateMachine
	{
		public TumblerClientRuntime Runtime
		{
			get; set;
		}
		public PaymentStateMachine(
			TumblerClientRuntime runtime)
		{
			if(runtime == null)
				throw new ArgumentNullException("runtime");
			Runtime = runtime;
		}




		public PaymentStateMachine(
			TumblerClientRuntime runtime,
			State state) : this(runtime)
		{
			if(state == null)
				return;
			if(state.NegotiationClientState != null)
			{
				StartCycle = state.NegotiationClientState.CycleStart;
				ClientChannelNegotiation = new ClientChannelNegotiation(runtime.TumblerParameters, state.NegotiationClientState);
			}
			if(state.PromiseClientState != null)
				PromiseClientSession = new PromiseClientSession(runtime.TumblerParameters.CreatePromiseParamaters(), state.PromiseClientState);
			if(state.SolverClientState != null)
				SolverClientSession = new SolverClientSession(runtime.TumblerParameters.CreateSolverParamaters(), state.SolverClientState);
			InvalidPhaseCount = state.InvalidPhaseCount;
		}

		public int InvalidPhaseCount
		{
			get; set;
		}

		public Tracker Tracker
		{
			get
			{
				return Runtime.Tracker;
			}
		}
		public ExternalServices Services
		{
			get
			{
				return Runtime.Services;
			}
		}

		public TumblerClients Clients
		{
			get; set;
		}
		public TumblerClient BobClient
		{
			get
			{
				return Clients.Bob;
			}
		}
		public TumblerClient AliceClient
		{
			get
			{
				return Clients.Alice;
			}
		}
		public ClassicTumblerParameters Parameters
		{
			get
			{
				return Runtime.TumblerParameters;
			}
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
		public IDestinationWallet DestinationWallet
		{
			get
			{
				return Runtime.DestinationWallet;
			}
		}
		public bool Cooperative
		{
			get
			{
				return Runtime.Cooperative;
			}
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
			public int InvalidPhaseCount
			{
				get; set;
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
			s.InvalidPhaseCount = InvalidPhaseCount;
			return s;
		}

		public void Update()
		{
			Clients = Clients ?? Runtime.CreateTumblerClients();
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

			Logs.Client.LogInformation("Updating cycle " + cycle.Start);

			Logs.Client.LogInformation("Phase " + Enum.GetName(typeof(CyclePhase), phase) + ", ending in " + (cycle.GetPeriods().GetPeriod(phase).End - height) + " blocks");


			var correlation = SolverClientSession == null ? 0 : GetCorrelation(SolverClientSession.EscrowedCoin);

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
						Logs.Client.LogInformation("Registered");
					}
					break;
				case CyclePhase.ClientChannelEstablishment:
					if(ClientChannelNegotiation.Status == TumblerClientSessionStates.WaitingTumblerClientTransactionKey)
					{
						var key = AliceClient.RequestTumblerEscrowKey(cycle.Start);
						ClientChannelNegotiation.ReceiveTumblerEscrowKey(key.PubKey, key.KeyIndex);
						//Client create the escrow
						var escrowTxOut = ClientChannelNegotiation.BuildClientEscrowTxOut();
						feeRate = GetFeeRate();

						Transaction clientEscrowTx = null;
						try
						{
							clientEscrowTx = Services.WalletService.FundTransaction(escrowTxOut, feeRate);
						}
						catch(NotEnoughFundsException ex)
						{
							Logs.Client.LogInformation($"Not enough funds in the wallet to tumble. Missing about {ex.Missing}. Denomination is {Parameters.Denomination}.");
							break;
						}

						var redeemDestination = Services.WalletService.GenerateAddress().ScriptPubKey;
						SolverClientSession = ClientChannelNegotiation.SetClientSignedTransaction(clientEscrowTx, redeemDestination);


						correlation = GetCorrelation(SolverClientSession.EscrowedCoin);

						Tracker.AddressCreated(cycle.Start, TransactionType.ClientEscrow, escrowTxOut.ScriptPubKey, correlation);
						Tracker.TransactionCreated(cycle.Start, TransactionType.ClientEscrow, clientEscrowTx.GetHash(), correlation);
						Services.BlockExplorerService.Track(escrowTxOut.ScriptPubKey);


						var redeemTx = SolverClientSession.CreateRedeemTransaction(feeRate);
						Tracker.AddressCreated(cycle.Start, TransactionType.ClientRedeem, redeemDestination, correlation);

						//redeemTx does not be to be recorded to the tracker, this is TrustedBroadcastService job

						Services.BroadcastService.Broadcast(clientEscrowTx);

						Services.TrustedBroadcastService.Broadcast(cycle.Start, TransactionType.ClientRedeem, correlation, redeemTx);

						Logs.Client.LogInformation("Client channel broadcasted");
					}
					else if(ClientChannelNegotiation.Status == TumblerClientSessionStates.WaitingSolvedVoucher)
					{
						TransactionInformation clientTx = GetTransactionInformation(SolverClientSession.EscrowedCoin, true);
						var state = ClientChannelNegotiation.GetInternalState();
						if(clientTx != null && clientTx.Confirmations >= cycle.SafetyPeriodDuration)
						{
							Logs.Client.LogInformation($"Client escrow reached {cycle.SafetyPeriodDuration} confirmations");
							//Client asks the public key of the Tumbler and sends its own
							var voucher = AliceClient.SignVoucher(new SignVoucherRequest
							{
								MerkleProof = clientTx.MerkleProof,
								Transaction = clientTx.Transaction,
								KeyReference = state.TumblerEscrowKeyReference,
								UnsignedVoucher = state.BlindedVoucher,
								Cycle = cycle.Start,
								ClientEscrowKey = state.ClientEscrowKey.PubKey
							});
							ClientChannelNegotiation.CheckVoucherSolution(voucher);
							Logs.Client.LogInformation($"Tumbler escrow voucher obtained");
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
						Logs.Client.LogInformation("Tumbler escrow broadcasted");
						//Tell to the block explorer we need to track that address (for checking if it is confirmed in payment phase)
						Services.BlockExplorerService.Track(PromiseClientSession.EscrowedCoin.ScriptPubKey);
						Tracker.AddressCreated(cycle.Start, TransactionType.TumblerEscrow, PromiseClientSession.EscrowedCoin.ScriptPubKey, correlation);
						Tracker.TransactionCreated(cycle.Start, TransactionType.TumblerEscrow, PromiseClientSession.EscrowedCoin.Outpoint.Hash, correlation);

						//Channel is done, now need to run the promise protocol to get valid puzzle
						var cashoutDestination = DestinationWallet.GetNewDestination();
						Tracker.AddressCreated(cycle.Start, TransactionType.TumblerCashout, cashoutDestination, correlation);

						feeRate = GetFeeRate();
						var sigReq = PromiseClientSession.CreateSignatureRequest(cashoutDestination, feeRate);
						var commiments = BobClient.SignHashes(cycle.Start, PromiseClientSession.Id, sigReq);
						var revelation = PromiseClientSession.Reveal(commiments);
						var proof = BobClient.CheckRevelation(cycle.Start, PromiseClientSession.Id, revelation);
						var puzzle = PromiseClientSession.CheckCommitmentProof(proof);
						SolverClientSession.AcceptPuzzle(puzzle);
						Logs.Client.LogInformation("Tumbler escrow puzzle obtained");
					}
					break;
				case CyclePhase.PaymentPhase:
					if(PromiseClientSession != null)
					{
						TransactionInformation tumblerTx = GetTransactionInformation(PromiseClientSession.EscrowedCoin, false);
						//Ensure the tumbler coin is confirmed before paying anything
						if(tumblerTx != null || tumblerTx.Confirmations >= cycle.SafetyPeriodDuration)
						{
							Logs.Client.LogInformation($"Client escrow reached {cycle.SafetyPeriodDuration} confirmations");

							if(SolverClientSession.Status == SolverClientStates.WaitingGeneratePuzzles)
							{
								feeRate = GetFeeRate();
								var puzzles = SolverClientSession.GeneratePuzzles();
								var commmitments = AliceClient.SolvePuzzles(cycle.Start, SolverClientSession.Id, puzzles);
								var revelation2 = SolverClientSession.Reveal(commmitments);
								var solutionKeys = AliceClient.CheckRevelation(cycle.Start, SolverClientSession.Id, revelation2);
								var blindFactors = SolverClientSession.GetBlindFactors(solutionKeys);
								var offerInformation = AliceClient.CheckBlindFactors(cycle.Start, SolverClientSession.Id, blindFactors);

								var offerSignature = SolverClientSession.SignOffer(offerInformation);

								var offerRedeem = SolverClientSession.CreateOfferRedeemTransaction(feeRate);
								//May need to find solution in the fulfillment transaction
								Services.BlockExplorerService.Track(offerRedeem.PreviousScriptPubKey);
								Tracker.AddressCreated(cycle.Start, TransactionType.ClientOfferRedeem, SolverClientSession.GetInternalState().RedeemDestination, correlation);
								Services.TrustedBroadcastService.Broadcast(cycle.Start, TransactionType.ClientOfferRedeem, correlation, offerRedeem);
								try
								{
									solutionKeys = AliceClient.FulfillOffer(cycle.Start, SolverClientSession.Id, offerSignature);
									SolverClientSession.CheckSolutions(solutionKeys);
									var tumblingSolution = SolverClientSession.GetSolution();
									var transaction = PromiseClientSession.GetSignedTransaction(tumblingSolution);
									Logs.Client.LogInformation("Got puzzle solution cooperatively from the tumbler");
									Services.TrustedBroadcastService.Broadcast(cycle.Start, TransactionType.TumblerCashout, correlation, new TrustedBroadcastRequest()
									{
										BroadcastAt = cycle.GetPeriods().ClientCashout.Start,
										Transaction = transaction
									});
									if(Cooperative)
									{
										var signature = SolverClientSession.SignEscape();
										AliceClient.GiveEscapeKey(cycle.Start, SolverClientSession.Id, signature);
										Logs.Client.LogInformation("Gave escape signature to the tumbler");
									}
								}
								catch(Exception ex)
								{
									Logs.Client.LogWarning("The tumbler did not gave puzzle solution cooperatively");
									Logs.Client.LogWarning(ex.ToString());
								}
							}
						}
					}
					break;
				case CyclePhase.ClientCashoutPhase:
					if(SolverClientSession != null)
					{
						//If the tumbler is uncooperative, he published solutions on the blockchain
						if(SolverClientSession.Status == SolverClientStates.WaitingPuzzleSolutions)
						{
							var transactions = Services.BlockExplorerService.GetTransactions(SolverClientSession.GetInternalState().OfferCoin.ScriptPubKey, false);
							if(transactions.Length != 0)
							{
								SolverClientSession.CheckSolutions(transactions.Select(t => t.Transaction).ToArray());
								Logs.Client.LogInformation("Puzzle solution recovered from tumbler's fulfill transaction");

								var tumblingSolution = SolverClientSession.GetSolution();
								var transaction = PromiseClientSession.GetSignedTransaction(tumblingSolution);
								Tracker.TransactionCreated(cycle.Start, TransactionType.TumblerCashout, transaction.GetHash(), correlation);
								Services.BroadcastService.Broadcast(transaction);
							}
						}
					}
					break;
			}
		}

		private uint GetCorrelation(ScriptCoin escrowCoin)
		{
			return new uint160(escrowCoin.Redeem.Hash.ToString()).GetLow32();
		}

		private TransactionInformation GetTransactionInformation(ICoin coin, bool withProof)
		{
			var tx = Services.BlockExplorerService
				.GetTransactions(coin.TxOut.ScriptPubKey, withProof)
				.FirstOrDefault(t => t.Transaction.Outputs.AsCoins().Any(c => c.Outpoint == coin.Outpoint));
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
