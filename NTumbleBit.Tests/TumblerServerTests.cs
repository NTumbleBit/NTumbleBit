using NBitcoin;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.Client.Tumbler;
using NTumbleBit.Client.Tumbler.Models;
using NTumbleBit.Client.Tumbler.Services;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.TumblerServer.Services;
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

		private FeeRate FeeRate = new FeeRate(50, 1);

		[Fact]
		public void TestCRUDDBReeze()
		{
			using(var server = TumblerServerTester.Create())
			{
				var repo = server.ServerContext.GetService<NTumbleBit.TumblerServer.Services.IRepository>();
				repo.UpdateOrInsert("a", "b", "c", (o, n) => n);
				var result = repo.Get<string>("a", "b");
				Assert.Equal("c", result);
				repo.UpdateOrInsert("a", "b", "d", (o, n) => n);
				result = repo.Get<string>("a", "b");
				Assert.Equal("d", result);
				repo.UpdateOrInsert("a", "c", "c", (o, n) => n);
				Assert.Equal(2, repo.List<string>("a").Length);
				repo.Delete<string>("a", "c");
				Assert.Equal(1, repo.List<string>("a").Length);
				repo.UpdateOrInsert("a", "c", "c", (o, n) => n);
				repo.Delete("a");
				Assert.Equal(0, repo.List<string>("a").Length);
			}
		}

		[Fact]
		public void CanCompleteCycleWithMachineState()
		{
			using(var server = TumblerServerTester.Create())
			{
				server.AliceNode.FindBlock(1);
				server.SyncNodes();
				server.TumblerNode.FindBlock(1);
				server.SyncNodes();
				server.BobNode.FindBlock(103);
				server.SyncNodes();

				var machine = server.ClientContext.PaymentMachineState;
				machine.Update();
				var cycle = machine.ClientChannelNegotiation.GetCycle();

				MineTo(server.AliceNode, cycle, CyclePhase.ClientChannelEstablishment);
				server.SyncNodes();


				var escrow1 = machine.AliceClient.RequestTumblerEscrowKey(machine.StartCycle);
				var escrow2 = machine.AliceClient.RequestTumblerEscrowKey(machine.StartCycle);
				Assert.Equal(0, escrow1.KeyIndex);
				Assert.Equal(1, escrow2.KeyIndex);
				machine.Update();

				//Wait the client escrow is confirmed
				server.AliceNode.FindBlock(2);
				server.SyncNodes();

				//Server does not track anything until Alice gives proof of the escrow
				Assert.Equal(0, server.ServerContext.BlockExplorer
						.GetTransactions(machine.SolverClientSession.EscrowedCoin.ScriptPubKey, false)
						.Count());

				machine.Update();

				//Server is now tracking Alice's escrow
				Assert.Equal(1, server.ServerContext.BlockExplorer
						.GetTransactions(machine.SolverClientSession.EscrowedCoin.ScriptPubKey, false)
						.Count());

				MineTo(server.AliceNode, cycle, CyclePhase.TumblerChannelEstablishment);
				server.SyncNodes();

				machine.Update();

				MineTo(server.TumblerNode, cycle, CyclePhase.PaymentPhase);
				server.SyncNodes();

				machine.Update();

				MineTo(server.TumblerNode, cycle, CyclePhase.PaymentPhase, true);
				server.SyncNodes();
				//Offer + Fulfill should be broadcasted
				var transactions = server.ServerContext.TrustedBroadcastService.TryBroadcast();
				Assert.Equal(2, transactions.Length);

				//Offer got malleated
				server.TumblerNode.Malleate(transactions[0].GetHash());
				var block = server.TumblerNode.FindBlock(1).First();
				Assert.Equal(2, block.Transactions.Count); //Offer get mined
				server.SyncNodes();

				//Fulfill get resigned and broadcasted
				transactions = server.ServerContext.TrustedBroadcastService.TryBroadcast();
				Assert.Equal(1, transactions.Length);
				block = server.TumblerNode.FindBlock(1).First();
				Assert.Equal(2, block.Transactions.Count); //Fulfill get mined		

				MineTo(server.TumblerNode, cycle, CyclePhase.ClientCashoutPhase);
				server.SyncNodes();
				machine.Update();

				transactions = server.ClientContext.UntrustedBroadcaster.TryBroadcast();
				Assert.Equal(1, transactions.Length);
				block = server.AliceNode.FindBlock().First();
				//Should contains client cashout
				Assert.Equal(2, block.Transactions.Count);

				//Just a sanity tests, this one contains escrow redeem and offer redeem, both of which should not be available now
				transactions = server.ClientContext.UntrustedBroadcaster.TryBroadcast();
				Assert.Equal(0, transactions.Length);
			}
		}


		[Fact]
		public void EscrowGetRedeemedIfTimeout()
		{
			using(var server = TumblerServerTester.Create())
			{
				server.AliceNode.FindBlock(1);
				server.SyncNodes();
				server.TumblerNode.FindBlock(1);
				server.SyncNodes();
				server.BobNode.FindBlock(103);
				server.SyncNodes();

				var machine = server.ClientContext.PaymentMachineState;
				machine.Update();
				var cycle = machine.ClientChannelNegotiation.GetCycle();

				MineTo(server.AliceNode, cycle, CyclePhase.ClientChannelEstablishment);
				server.SyncNodes();

				machine.Update();

				//Wait the client escrow is confirmed
				server.AliceNode.FindBlock(2);
				server.SyncNodes();

				//Server does not track anything until Alice gives proof of the escrow
				Assert.Equal(0, server.ServerContext.BlockExplorer
						.GetTransactions(machine.SolverClientSession.EscrowedCoin.ScriptPubKey, false)
						.Count());

				machine.Update();

				//Server is now tracking Alice's escrow
				Assert.Equal(1, server.ServerContext.BlockExplorer
						.GetTransactions(machine.SolverClientSession.EscrowedCoin.ScriptPubKey, false)
						.Count());

				MineTo(server.AliceNode, cycle, CyclePhase.TumblerChannelEstablishment);
				server.SyncNodes();

				machine.Update();

				//Client Refund broadcasted exactly when we are at ClientCashoutPhase + SafetyPeriodDuration
				MineTo(server.AliceNode, cycle, CyclePhase.ClientCashoutPhase, offset: cycle.SafetyPeriodDuration - 1);
				var broadcasted = server.ClientContext.TrustedBroadcastService.TryBroadcast();
				Assert.Equal(0, broadcasted.Length);
				MineTo(server.AliceNode, cycle, CyclePhase.ClientCashoutPhase, offset: cycle.SafetyPeriodDuration);
				broadcasted = server.ClientContext.TrustedBroadcastService.TryBroadcast();
				Assert.Equal(1, broadcasted.Length);
				////////////////////////////////////////////////////////////////////////////////

				//Tumbler Refund broadcasted exactly when we are at ClientCashoutPhase + SafetyPeriodDuration
				MineTo(server.AliceNode, cycle, CyclePhase.ClientCashoutPhase, end: true, offset: cycle.SafetyPeriodDuration - 1);
				broadcasted = server.ServerContext.TrustedBroadcastService.TryBroadcast();
				Assert.Equal(0, broadcasted.Length);
				MineTo(server.AliceNode, cycle, CyclePhase.ClientCashoutPhase, end: true, offset: cycle.SafetyPeriodDuration);
				broadcasted = server.ServerContext.TrustedBroadcastService.TryBroadcast();
				Assert.Equal(1, broadcasted.Length);
				////////////////////////////////////////////////////////////////////////////////
			}
		}

		private void MineTo(CoreNode node, CycleParameters cycle, CyclePhase phase, bool end = false, int offset = 0)
		{
			var height = node.CreateRPCClient().GetBlockCount();
			var periodStart = end ? cycle.GetPeriods().GetPeriod(phase).End : cycle.GetPeriods().GetPeriod(phase).Start;
			var blocksToFind = periodStart - height + offset;
			if(blocksToFind <= 0)
				return;

			node.FindBlock(blocksToFind);
		}
	}
}
