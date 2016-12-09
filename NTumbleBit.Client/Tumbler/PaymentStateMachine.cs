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
			ExternalServices services,
			int startCycle)
		{
			Parameters = parameters;
			StartCycle = startCycle;
			AliceClient = client;
			BobClient = client;
			Services = services;
			ClientSession = new TumblerClientSession(Parameters, startCycle);
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
		public TumblerClientSession ClientSession
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

		public void Update()
		{
			switch(ClientSession.Status)
			{
				case TumblerClientSessionStates.WaitingVoucher:
					/////////////////////////////<Registration>/////////////////////////
					//Client asks for voucher
					var voucherResponse = BobClient.AskUnsignedVoucher();
					//Client ensures he is in the same cycle as the tumbler (would fail if one tumbler or client's chain isn't sync)
					var cycle = Parameters.CycleGenerator.GetCycle(voucherResponse.Cycle);
					var expectedCycle = Parameters.CycleGenerator.GetRegistratingCycle(Services.BlockExplorerService.GetCurrentHeight());
					Assert(expectedCycle.Start == cycle.Start, "invalid-phase");
					//Saving the voucher for later
					ClientSession.ReceiveUnsignedVoucher(voucherResponse.UnsignedVoucher);
					/////////////////////////////</Registration>/////////////////////////
					return;
				case TumblerClientSessionStates.WaitingGenerateClientTransactionKeys:
					/////////////////////////////<ClientChannel>/////////////////////////
					//Client asks the public key of the Tumbler and sends its own
					var aliceEscrowInformation = ClientSession.GenerateClientTransactionKeys();
					var key = AliceClient.RequestTumblerEscrowKey(aliceEscrowInformation);
					ClientSession.ReceiveTumblerEscrowKey(key);
					return;
				case TumblerClientSessionStates.WaitingClientTransaction:
					//Client create the escrow
					var txout = ClientSession.BuildClientEscrowTxOut();
					var clientEscrowTx = Services.WalletService.FundTransaction(txout, GetFeeRate());
					Services.BlockExplorerService.Track(txout.ScriptPubKey);
					Services.BroadcastService.Broadcast(clientEscrowTx);
					SolverClientSession = ClientSession.SetClientSignedTransaction(clientEscrowTx);
					return;
				case TumblerClientSessionStates.WaitingSolvedVoucher:
					var voucher = AliceClient.ClientChannelConfirmed(SolverClientSession.EscrowedCoin.Outpoint.Hash);
					ClientSession.CheckVoucherSolution(voucher);
					/////////////////////////////</ClientChannel>/////////////////////////
					return;
				case TumblerClientSessionStates.WaitingGenerateTumblerTransactionKey:
					/////////////////////////////<TumblerChannel>/////////////////////////
					//Client asks the Tumbler to make a channel
					var bobEscrowInformation = ClientSession.GenerateTumblerTransactionKey();
					var tumblerInformation = BobClient.OpenChannel(bobEscrowInformation);
					PromiseClientSession = ClientSession.ReceiveTumblerEscrowedCoin(tumblerInformation);
					//Channel is done, now need to run the promise protocol to get valid puzzle
					var cashoutDestination = Services.WalletService.GenerateAddress();
					var sigReq = PromiseClientSession.CreateSignatureRequest(cashoutDestination, GetFeeRate());
					var commiments = BobClient.SignHashes(PromiseClientSession.Id, sigReq);
					var revelation = PromiseClientSession.Reveal(commiments);
					var proof = BobClient.CheckRevelation(PromiseClientSession.Id, revelation);
					var puzzle = PromiseClientSession.CheckCommitmentProof(proof);
					SolverClientSession.AcceptPuzzle(puzzle);
					/////////////////////////////</TumblerChannel>/////////////////////////
					return;
				case TumblerClientSessionStates.PromisePhase:
					switch(SolverClientSession.Status)
					{
						case SolverClientStates.WaitingGeneratePuzzles:
							/////////////////////////////<Payment>/////////////////////////
							//Client pays for the puzzle
							var puzzles = SolverClientSession.GeneratePuzzles();
							var commmitments = AliceClient.SolvePuzzles(SolverClientSession.Id, puzzles);
							var revelation2 = SolverClientSession.Reveal(commmitments);
							var solutionKeys = AliceClient.CheckRevelation(SolverClientSession.Id, revelation2);
							var blindFactors = SolverClientSession.GetBlindFactors(solutionKeys);
							var fullfillKey = AliceClient.CheckBlindFactors(SolverClientSession.Id, blindFactors);
							var offer = SolverClientSession.CreateOfferTransaction(fullfillKey, GetFeeRate());
							Services.BlockExplorerService.Track(offer.Outputs[0].ScriptPubKey);
							AliceClient.FullfillOffer(SolverClientSession.Id, offer);
							/////////////////////////////</Payment>/////////////////////////
							return;
						case SolverClientStates.WaitingPuzzleSolutions:
							/////////////////////////////<ClientCashout>/////////////////////////
							var txs = Services.BlockExplorerService.GetTransactions(SolverClientSession.GetOfferScript().Hash.ScriptPubKey);
							SolverClientSession.CheckSolutions(txs.Select(t => t.Transaction).ToArray());
							var tumblingSolution = SolverClientSession.GetSolution();
							var transaction = PromiseClientSession.GetSignedTransaction(tumblingSolution);
							Services.BroadcastService.Broadcast(transaction);
							/////////////////////////////</ClientCashout>/////////////////////////
							return;
						default:
							throw new NotSupportedException(SolverClientSession.Status.ToString());
					}
				default:
					throw new NotSupportedException(ClientSession.Status.ToString());
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
