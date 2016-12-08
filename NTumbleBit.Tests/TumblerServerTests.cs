using NBitcoin;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.Client.Tumbler.Models;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.TumblerServer.Services.RPCServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
				var clientSession = new TumblerClientSession(parameters, cycle.Start);
				clientSession.ReceiveUnsignedVoucher(voucherResponse.UnsignedVoucher);
				/////////////////////////////</Registration>/////////////////////////


				//Client waits until client channel establishment phase
				MineTo(server.AliceNode, cycle, CyclePhase.ClientChannelEstablishment);
				server.SyncNodes();
				///////////////

				/////////////////////////////<ClientChannel>/////////////////////////
				//Client asks the public key of the Tumbler and sends its own
				var aliceEscrowInformation = clientSession.GenerateClientTransactionKeys();
				var key = aliceClient.RequestTumblerEscrowKey(aliceEscrowInformation);
				clientSession.ReceiveTumblerEscrowKey(key);
				//Client create the escrow
				var clientWallet = new RPCWalletService(bobRPC);
				var txout = clientSession.BuildClientEscrowTxOut();
				var clientEscrowTx = clientWallet.FundTransaction(txout, FeeRate);
				bobRPC.SendRawTransaction(clientEscrowTx);
				server.BobNode.FindBlock(2);
				server.SyncNodes();
				clientSession.SetClientSignedTransaction(clientEscrowTx);
				//Server solves the puzzle
				var voucher = aliceClient.SolveVoucher(clientEscrowTx.GetHash());
				clientSession.CheckVoucherSolution(voucher);
				/////////////////////////////</ClientChannel>/////////////////////////

				//Client waits until tumbler channel establishment phase
				MineTo(server.AliceNode, cycle, CyclePhase.TumblerChannelEstablishment);
				server.SyncNodes();
				///////////////

				/////////////////////////////<TumblerChannel>/////////////////////////
				//Client asks the Tumbler to make a channel
				var bobEscrowInformation = clientSession.GenerateTumblerTransactionKey();
				var tumblerInformation = bobClient.OpenChannel(bobEscrowInformation);
				var escrow = clientSession.ReceiveTumblerEscrowInformation(tumblerInformation);
				//Channel is done, now need to run the promise protocol to get valid puzzle
				var cashoutDestination = clientWallet.GenerateAddress();
				var sigReq = clientSession.PromiseClientSession.CreateSignatureRequest(escrow, cashoutDestination, FeeRate);
				var commiments = bobClient.SignHashes(clientSession.GetTumblerChannelId(), sigReq);
				var revelation = clientSession.PromiseClientSession.Reveal(commiments);
				var proof = bobClient.CheckRevelation(clientSession.GetTumblerChannelId(), revelation);
				var puzzle = clientSession.PromiseClientSession.CheckCommitmentProof(proof);
				clientSession.SolverClientSession.AcceptPuzzle(puzzle);
				/////////////////////////////</TumblerChannel>/////////////////////////

				//Client waits until payment phase
				MineTo(server.AliceNode, cycle, CyclePhase.PaymentPhase);
				server.SyncNodes();
				///////////////

				/////////////////////////////<Payment>/////////////////////////
				//Client pays for the puzzle
				var puzzles = clientSession.SolverClientSession.GeneratePuzzles();
				var commmitments = aliceClient.SolvePuzzles(clientSession.GetClientChannelId(), puzzles);
				var revelation2 = clientSession.SolverClientSession.Reveal(commmitments);
				var solutionKeys = aliceClient.CheckRevelation(clientSession.GetClientChannelId(), revelation2);
				var blindFactors = clientSession.SolverClientSession.GetBlindFactors(solutionKeys);
				
				//clientSession.SolverClientSession.CreateOfferScript(new PuzzleSolver.PaymentCashoutContext())
				aliceClient.CheckBlindFactors(clientSession.GetClientChannelId(), blindFactors);
				/////////////////////////////</Payment>/////////////////////////
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
