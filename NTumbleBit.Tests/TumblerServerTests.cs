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

		FeeRate FeeRate = new FeeRate(50, 1);

		[Fact]
		public void TestCRUDDBReeze()
		{
			using(var server = TumblerServerTester.Create())
			{
				var repo = server.GetService<IRepository>();
				repo.Add("a", "b", "c");
				var result = repo.Get<string>("a", "b");
				Assert.Equal("c", result);
				repo.Add("a", "b", "d");
				result = repo.Get<string>("a", "b");
				Assert.Equal("d", result);
				repo.Add("a", "c", "c");
				Assert.Equal(2, repo.List<string>("a").Length);
				repo.Delete<string>("a", "c");
				Assert.Equal(1, repo.List<string>("a").Length);
				repo.Add("a", "c", "c");
				repo.Delete("a");
				Assert.Equal(0, repo.List<string>("a").Length);
			}
		}

		[Fact]
		public void CanCompleteCycleWithMachineState()
		{
			using(var server = TumblerServerTester.Create())
			{
				var rpc = server.AliceNode.CreateRPCClient();
				server.AliceNode.FindBlock(1);
				server.SyncNodes();
				server.TumblerNode.FindBlock(1);
				server.SyncNodes();
				server.BobNode.FindBlock(103);
				server.SyncNodes();


				var aliceClient = server.CreateTumblerClient();
				var parameters = aliceClient.GetTumblerParameters();
				var cycle = parameters.CycleGenerator.GetCycle(rpc.GetBlockCount());

				var trustedServerBroadcaster = (TumblerServer.Services.RPCServices.RPCTrustedBroadcastService)server.ExtenalServices.TrustedBroadcastService;
				var serverBlockExplorer = (TumblerServer.Services.RPCServices.RPCBlockExplorerService)server.ExtenalServices.BlockExplorerService;

				var machine = new PaymentStateMachine(parameters, aliceClient, new NTumbleBit.Client.Tumbler.Services.ExternalServices()
				{
					BlockExplorerService = new NTumbleBit.Client.Tumbler.Services.RPCServices.RPCBlockExplorerService(rpc),
					BroadcastService = new NTumbleBit.Client.Tumbler.Services.RPCServices.RPCBroadcastService(rpc),
					FeeService = new NTumbleBit.Client.Tumbler.Services.RPCServices.RPCFeeService(rpc),
					TrustedBroadcastService = new NTumbleBit.Client.Tumbler.Services.RPCServices.RPCTrustedBroadcastService(rpc),
					WalletService = new NTumbleBit.Client.Tumbler.Services.RPCServices.RPCWalletService(rpc)
				});

				var trustedClientBroadcaster = (NTumbleBit.Client.Tumbler.Services.RPCServices.RPCTrustedBroadcastService)machine.Services.TrustedBroadcastService;

				machine.Update();

				MineTo(server.AliceNode, cycle, CyclePhase.ClientChannelEstablishment);
				server.SyncNodes();

				machine.Update();

				//Wait the client escrow is confirmed
				server.AliceNode.FindBlock(2);
				server.SyncNodes();

				//Server does not track anything until Alice gives proof of the escrow
				Assert.Equal(0, serverBlockExplorer
						.GetTransactions(machine.SolverClientSession.EscrowedCoin.ScriptPubKey, false)
						.Count());

				machine.Update();

				//Server is now tracking Alice's escrow
				Assert.Equal(1, serverBlockExplorer
						.GetTransactions(machine.SolverClientSession.EscrowedCoin.ScriptPubKey, false)
						.Count());

				MineTo(server.AliceNode, cycle, CyclePhase.TumblerChannelEstablishment);
				server.SyncNodes();

				machine.Update();

				MineTo(server.TumblerNode, cycle, CyclePhase.PaymentPhase);
				server.SyncNodes();

				machine.Update();

				MineTo(server.AliceNode, cycle, CyclePhase.PaymentPhase, true);
				server.SyncNodes();

				//Offer + Fullfill should be broadcasted
				var transactions = trustedServerBroadcaster.TryBroadcast();
				Assert.Equal(2, transactions.Length);

				//Offer got malleated
				server.TumblerNode.Malleate(transactions[0].GetHash());
				server.TumblerNode.FindBlock(1);
				server.SyncNodes();

				//Fullfill get resigned and broadcasted
				transactions = trustedServerBroadcaster.TryBroadcast();
				Assert.Equal(1, transactions.Length);

				MineTo(server.AliceNode, cycle, CyclePhase.ClientCashoutPhase);
				server.SyncNodes();

				machine.Update();
				Thread.Sleep(1000);
				server.AliceNode.FindBlock();
				var bestBlock = server.AliceNode.CreateRPCClient().GetBlock(server.AliceNode.CreateRPCClient().GetBlockCount());
				//Should contains cashout
				Assert.Equal(2, bestBlock.Transactions.Count);

				//Just a sanity tests, this one contains escrow redeem and offer redeem, both of which should not be available now
				transactions = trustedClientBroadcaster.TryBroadcast();
				Assert.Equal(0, transactions.Length);
			}
		}

		private void MineTo(CoreNode node, CycleParameters cycle, CyclePhase phase, bool end = false)
		{
			var height = node.CreateRPCClient().GetBlockCount();
			var periodStart = end ? cycle.GetPeriods().GetPeriod(phase).End : cycle.GetPeriods().GetPeriod(phase).Start;
			var blocksToFind = periodStart - height;
			if(blocksToFind <= 0)
				return;

			node.FindBlock(blocksToFind);
		}
	}
}
