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
				var repo = server.ServerContext.GetService<TumblerServer.Services.IRepository>();
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

				var dbreezeRepo = (TumblerServer.Services.DBreezeRepository)repo;

				var beforeOpened = dbreezeRepo.OpenedEngine;
				Random r = new Random();
				for(int i = 0; i < 100; i++)
				{
					var p = r.Next(0, 100);
					repo.UpdateOrInsert(p.ToString(), "eh", "q", (a, b) => a);
					Assert.True(dbreezeRepo.OpenedEngine <= dbreezeRepo.MaxOpenedEngine);
				}
			}
		}

		[Fact]
		public void CanUseTracker()
		{
			using(var server = TumblerServerTester.Create())
			{
				var tracker = server.ServerContext.GetService<TumblerServer.Tracker>();

				var address = new Key().ScriptPubKey;
				var address2 = new Key().ScriptPubKey;
				var address3 = new Key().ScriptPubKey;

				var h = new Transaction().GetHash();
				tracker.AddressCreated(1, TumblerServer.TransactionType.ClientEscrow, address);
				tracker.AddressCreated(1, TumblerServer.TransactionType.ClientFulfill, address2);
				tracker.TransactionCreated(1, TumblerServer.TransactionType.ClientEscrow, h);

				Assert.Equal(0, tracker.GetRecords(2).Length);
				Assert.Equal(3, tracker.GetRecords(1).Length);

				Assert.NotNull(tracker.Search(address));
				Assert.Null(tracker.Search(address3));
			}
		}


		[Fact]
		public void CanCompleteCycleWithMachineState()
		{
			CanCompleteCycleWithMachineStateCore(false, true);
			CanCompleteCycleWithMachineStateCore(true, false);
			CanCompleteCycleWithMachineStateCore(false, false);
			CanCompleteCycleWithMachineStateCore(true, true);
		}


		public void CanCompleteCycleWithMachineStateCore(bool cooperativeClient, bool cooperativeTumbler)
		{
			using(var server = TumblerServerTester.Create())
			{
				server.ServerContext.TumblerConfiguration.NonCooperative = !cooperativeTumbler;

				server.AliceNode.FindBlock(1);
				server.SyncNodes();
				server.TumblerNode.FindBlock(1);
				server.SyncNodes();
				server.BobNode.FindBlock(103);
				server.SyncNodes();

				var machine = server.ClientContext.PaymentMachineState;
				machine.NonCooperative = !cooperativeClient;

				var serverTracker = server.ServerContext.GetService<TumblerServer.Tracker>();
				var clientTracker = machine.Tracker;


				machine.Update();
				var cycle = machine.ClientChannelNegotiation.GetCycle();

				MineTo(server.AliceNode, cycle, CyclePhase.ClientChannelEstablishment);
				server.SyncNodes();


				var escrow1 = machine.AliceClient.RequestTumblerEscrowKey(machine.StartCycle);
				var escrow2 = machine.AliceClient.RequestTumblerEscrowKey(machine.StartCycle);
				Assert.Equal(0, escrow1.KeyIndex);
				Assert.Equal(1, escrow2.KeyIndex);
				machine.Update();

				clientTracker.AssertKnown(TransactionType.ClientEscrow, machine.SolverClientSession.EscrowedCoin.ScriptPubKey);
				clientTracker.AssertKnown(TransactionType.ClientEscrow, machine.SolverClientSession.EscrowedCoin.Outpoint.Hash);

				//Wait the client escrow is confirmed
				server.AliceNode.FindBlock(2);
				server.SyncNodes();

				//Server does not track anything until Alice gives proof of the escrow
				Assert.Equal(0, server.ServerContext.BlockExplorer
						.GetTransactions(machine.SolverClientSession.EscrowedCoin.ScriptPubKey, false)
						.Count());
				serverTracker.AssertNotKnown(machine.SolverClientSession.EscrowedCoin.ScriptPubKey);

				machine.Update();


				//Server is now tracking Alice's escrow
				Assert.Equal(1, server.ServerContext.BlockExplorer
						.GetTransactions(machine.SolverClientSession.EscrowedCoin.ScriptPubKey, false)
						.Count());
				serverTracker.AssertKnown(TumblerServer.TransactionType.ClientEscrow, machine.SolverClientSession.EscrowedCoin.ScriptPubKey);
				serverTracker.AssertKnown(TumblerServer.TransactionType.ClientEscrow, machine.SolverClientSession.EscrowedCoin.Outpoint.Hash);
				//


				MineTo(server.AliceNode, cycle, CyclePhase.TumblerChannelEstablishment);
				server.SyncNodes();

				machine.Update();


				serverTracker.AssertKnown(TumblerServer.TransactionType.TumblerEscrow, machine.PromiseClientSession.EscrowedCoin.ScriptPubKey);
				serverTracker.AssertKnown(TumblerServer.TransactionType.TumblerEscrow, machine.PromiseClientSession.EscrowedCoin.Outpoint.Hash);
				clientTracker.AssertKnown(TransactionType.TumblerEscrow, machine.PromiseClientSession.EscrowedCoin.ScriptPubKey);
				clientTracker.AssertKnown(TransactionType.TumblerEscrow, machine.PromiseClientSession.EscrowedCoin.Outpoint.Hash);

				MineTo(server.TumblerNode, cycle, CyclePhase.PaymentPhase);
				server.SyncNodes();

				machine.Update();

				Block block = null;
				if(cooperativeClient && cooperativeTumbler)
					block = server.TumblerNode.FindBlock(1).First();

				MineTo(server.TumblerNode, cycle, CyclePhase.PaymentPhase, true);
				server.SyncNodes();

				Transaction[] transactions = null;
				if(!cooperativeClient || !cooperativeTumbler)
				{
					//Offer + Fulfill should be broadcasted
					transactions = server.ServerContext.TrustedBroadcastService.TryBroadcast();
					Assert.Equal(2, transactions.Length);
					var unmalleatedTransactions = transactions;

					serverTracker.AssertKnown(TumblerServer.TransactionType.ClientOffer, transactions[0].GetHash());
					serverTracker.AssertKnown(TumblerServer.TransactionType.ClientFulfill, transactions[1].GetHash());

					//Offer got malleated
					server.TumblerNode.Malleate(transactions[0].GetHash());
					block = server.TumblerNode.FindBlock(1).First();
					Assert.Equal(2, block.Transactions.Count); //Offer get mined
					server.SyncNodes();
					var malleatedOffer = block.Transactions[1];

					//Fulfill get resigned and broadcasted
					transactions = server.ServerContext.TrustedBroadcastService.TryBroadcast();
					Assert.Equal(1, transactions.Length);
					block = server.TumblerNode.FindBlock(1).First();
					Assert.Equal(2, block.Transactions.Count); //Fulfill get mined

					var malleatedFulfill = block.Transactions[1];

					Assert.NotEqual(unmalleatedTransactions[0].GetHash(), malleatedOffer.GetHash());
					Assert.NotEqual(unmalleatedTransactions[1].GetHash(), malleatedFulfill.GetHash());
					serverTracker.AssertNotKnown(malleatedOffer.GetHash()); //Offer got sneakily malleated, so the server did not broadcasted this version
					serverTracker.AssertKnown(TumblerServer.TransactionType.ClientFulfill, malleatedFulfill.GetHash());
				}
				else
				{
					//Escape should be broadcasted
					Assert.Equal(2, block.Transactions.Count);
				
					serverTracker.AssertKnown(TumblerServer.TransactionType.ClientEscape, block.Transactions[1].GetHash());
					serverTracker.AssertKnown(TumblerServer.TransactionType.ClientEscape, block.Transactions[1].Outputs[0].ScriptPubKey);
				}

				MineTo(server.TumblerNode, cycle, CyclePhase.ClientCashoutPhase);
				server.SyncNodes();
				machine.Update();

				if(cooperativeTumbler)
					//Received the solution out of blockchain, so the transaction should have been planned in advance
					transactions = server.ClientContext.TrustedBroadcastService.TryBroadcast();
				else
					//Received the solution from theblockchain, the transaction has not been planned in advance
					transactions = server.ClientContext.UntrustedBroadcaster.TryBroadcast();
				Assert.Equal(1, transactions.Length);
				block = server.AliceNode.FindBlock().First();
				//Should contains TumblerCashout
				Assert.Equal(2, block.Transactions.Count);

				clientTracker.AssertKnown(TransactionType.TumblerCashout, block.Transactions[1].GetHash());
				clientTracker.AssertKnown(TransactionType.TumblerCashout, block.Transactions[1].Outputs[0].ScriptPubKey);

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
