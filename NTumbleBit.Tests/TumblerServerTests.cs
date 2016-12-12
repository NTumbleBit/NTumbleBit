using NBitcoin;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.Client.Tumbler.Models;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.TumblerServer.Services.RPCServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NTumbleBit.Tests
{
	public class TumblerServerTests
	{
		[Fact]
		public void CanGetParameters()
		{
			using(var server = TumblerServerTester.Create())
			{
				var client = server.CreateTumblerClient();
				var parameters = client.GetTumblerParameters();
				Assert.NotNull(parameters.ServerKey);
				Assert.NotEqual(0, parameters.RealTransactionCount);
				Assert.NotEqual(0, parameters.FakeTransactionCount);
				Assert.NotNull(parameters.FakeFormat);
				Assert.True(parameters.FakeFormat != uint256.Zero);
			}
		}

		FeeRate FeeRate = new FeeRate(50, 1);
		[Fact]
		public void CanCompleteCycle()
		{
			using(var server = TumblerServerTester.Create())
			{
				var bobRPC = server.BobNode.CreateRPCClient();
				server.BobNode.FindBlock(1);
				server.TumblerNode.FindBlock(1);
				server.AliceNode.FindBlock(103);
				server.SyncNodes();

				var bobClient = server.CreateTumblerClient();
				var aliceClient = server.CreateTumblerClient();

				//Client get fix tumbler parameters
				var parameters = aliceClient.GetTumblerParameters();
				///////////////////////////////////

				/////////////////////////////<Registration>/////////////////////////
				//Client asks for voucher
				var voucherResponse = bobClient.AskUnsignedVoucher();
				//Client ensures he is in the same cycle as the tumbler (would fail if one tumbler or client's chain isn't sync)
				var cycle = parameters.CycleGenerator.GetCycle(voucherResponse.Cycle);
				var expectedCycle = parameters.CycleGenerator.GetRegistratingCycle(bobRPC.GetBlockCount());
				Assert.Equal(expectedCycle.Start, cycle.Start);

				//Saving the voucher for later
				var clientNegotiation = new ClientChannelNegotiation(parameters, cycle.Start);
				clientNegotiation.ReceiveUnsignedVoucher(voucherResponse.UnsignedVoucher);
				/////////////////////////////</Registration>/////////////////////////


				//Client waits until client channel establishment phase
				MineTo(server.AliceNode, cycle, CyclePhase.ClientChannelEstablishment);
				server.SyncNodes();
				///////////////

				/////////////////////////////<ClientChannel>/////////////////////////
				//Client asks the public key of the Tumbler and sends its own
				var aliceEscrowInformation = clientNegotiation.GenerateClientTransactionKeys();
				var key = aliceClient.RequestTumblerEscrowKey(aliceEscrowInformation);
				clientNegotiation.ReceiveTumblerEscrowKey(key);
				//Client create the escrow
				var clientWallet = new RPCWalletService(bobRPC);
				var clientBlockExplorer = new RPCBlockExplorerService(bobRPC);

				var txout = clientNegotiation.BuildClientEscrowTxOut();
				var clientEscrowTx = clientWallet.FundTransaction(txout, FeeRate);

				var clientBroadcastLater = new RPCLockTimedBroadcastService(bobRPC);
				bobRPC.SendRawTransaction(clientEscrowTx);
				server.BobNode.FindBlock(2);
				server.SyncNodes();
				var solverClientSession = clientNegotiation.SetClientSignedTransaction(clientEscrowTx);
				clientBroadcastLater.BroadcastLater(solverClientSession.CreateRedeemTransaction(FeeRate, new Key().ScriptPubKey));

				//Checking that the redeem transaction of the client escrow has proper validation time
				var redeem = solverClientSession.CreateRedeemTransaction(FeeRate, new Key().ScriptPubKey);
				var firstExpectedRedeemableBlock = clientNegotiation.GetCycle().GetPeriods().TumblerCashout.End + clientNegotiation.GetCycle().SafetyPeriodDuration;
				Assert.True(redeem.IsFinal(DateTimeOffset.UtcNow, firstExpectedRedeemableBlock));
				Assert.True(redeem.IsFinal(DateTimeOffset.UtcNow, firstExpectedRedeemableBlock + 1));
				Assert.False(redeem.IsFinal(DateTimeOffset.UtcNow, firstExpectedRedeemableBlock - 1));
				//Server solves the puzzle
				var voucher = aliceClient.ClientChannelConfirmed(clientEscrowTx.GetHash());
				clientNegotiation.CheckVoucherSolution(voucher);
				/////////////////////////////</ClientChannel>/////////////////////////

				//Client waits until tumbler channel establishment phase
				MineTo(server.AliceNode, cycle, CyclePhase.TumblerChannelEstablishment);
				server.SyncNodes();
				///////////////

				/////////////////////////////<TumblerChannel>/////////////////////////
				//Client asks the Tumbler to make a channel
				var bobEscrowInformation = clientNegotiation.GetOpenChannelRequest();
				var tumblerInformation = bobClient.OpenChannel(bobEscrowInformation);
				var promiseClientSession = clientNegotiation.ReceiveTumblerEscrowedCoin(tumblerInformation);
				//Channel is done, now need to run the promise protocol to get valid puzzle
				var cashoutDestination = clientWallet.GenerateAddress();
				var sigReq = promiseClientSession.CreateSignatureRequest(cashoutDestination, FeeRate);
				var commiments = bobClient.SignHashes(promiseClientSession.Id, sigReq);
				var revelation = promiseClientSession.Reveal(commiments);
				var proof = bobClient.CheckRevelation(promiseClientSession.Id, revelation);
				var puzzle = promiseClientSession.CheckCommitmentProof(proof);
				solverClientSession.AcceptPuzzle(puzzle);
				//Checking that the redeem transaction of the tumbler escrow has proper validation time
				var tumblerPromiseSession = server.TumblerRepository.GetPromiseServerSession(promiseClientSession.Id);
				redeem = tumblerPromiseSession.CreateRedeemTransaction(FeeRate, new Key().ScriptPubKey);
				firstExpectedRedeemableBlock = clientNegotiation.GetCycle().GetPeriods().ClientCashout.End + clientNegotiation.GetCycle().SafetyPeriodDuration;
				Assert.True(redeem.IsFinal(DateTimeOffset.UtcNow, firstExpectedRedeemableBlock));
				Assert.True(redeem.IsFinal(DateTimeOffset.UtcNow, firstExpectedRedeemableBlock + 1));
				Assert.False(redeem.IsFinal(DateTimeOffset.UtcNow, firstExpectedRedeemableBlock - 1));
				/////////////////////////////</TumblerChannel>/////////////////////////

				//Client waits until payment phase
				MineTo(server.AliceNode, cycle, CyclePhase.PaymentPhase);
				server.SyncNodes();
				///////////////

				/////////////////////////////<Payment>/////////////////////////
				//Client pays for the puzzle
				var puzzles = solverClientSession.GeneratePuzzles();
				var commmitments = aliceClient.SolvePuzzles(solverClientSession.Id, puzzles);
				var revelation2 = solverClientSession.Reveal(commmitments);
				var solutionKeys = aliceClient.CheckRevelation(solverClientSession.Id, revelation2);
				var blindFactors = solverClientSession.GetBlindFactors(solutionKeys);
				var offerInformation = aliceClient.CheckBlindFactors(solverClientSession.Id, blindFactors);
				var offerSignature = solverClientSession.SignOffer(offerInformation);
				var offerScriptPubKey = solverClientSession.GetOfferScriptPubKey();
				clientBlockExplorer.Track(offerScriptPubKey);
				var fullfill = aliceClient.FullfillOffer(solverClientSession.Id, offerSignature);				
				/////////////////////////////</Payment>/////////////////////////

				//Client waits until can cashout
				MineTo(server.AliceNode, cycle, CyclePhase.ClientCashoutPhase);
				server.SyncNodes();
				///////////////

				/////////////////////////////<ClientCashout>/////////////////////////
				solverClientSession.CheckSolutions(fullfill);
				var tumblingSolution = solverClientSession.GetSolution();
				var transaction = promiseClientSession.GetSignedTransaction(tumblingSolution);
				Assert.True(transaction.Inputs.AsIndexedInputs().First().VerifyScript(promiseClientSession.EscrowedCoin));
				/////////////////////////////</ClientCashout>/////////////////////////
			}
		}

		private void MineTo(CoreNode node, CycleParameters cycle, CyclePhase phase)
		{
			var height = node.CreateRPCClient().GetBlockCount();
			var periodStart = cycle.GetPeriods().GetPeriod(phase).Start;
			var blocksToFind = periodStart - height;
			if(blocksToFind <= 0)
				return;
			
			node.FindBlock(blocksToFind);
		}
	}
}
