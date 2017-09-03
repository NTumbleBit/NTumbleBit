using NBitcoin;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.ClassicTumbler.Client;
using NTumbleBit.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
				var client = server.ClientRuntime.CreateTumblerClient(0);
				var parameters = client.GetTumblerParameters();
				Assert.NotNull(parameters.ServerKey);
				Assert.NotEqual(0, parameters.RealTransactionCount);
				Assert.NotEqual(0, parameters.FakeTransactionCount);
				Assert.NotNull(parameters.FakeFormat);
				Assert.True(parameters.FakeFormat != uint256.Zero);
				Assert.Equal(RsaKey.KeySize, parameters.VoucherKey.PublicKey.GetKeySize());
			}
		}

		[Fact]
		public void TestStandard()
		{
			using(var server = TumblerServerTester.Create("TestStandard", true))
			{
			}
		}

		private FeeRate FeeRate = new FeeRate(50, 1);

		[Fact]
		public void TestCRUDDBReeze()
		{
			using(var server = TumblerServerTester.Create())
			{
				var repo = server.ServerRuntime.Repository;
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

				var dbreezeRepo = (DBreezeRepository)repo;

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
				var tracker = server.ServerRuntime.Tracker;

				var address = new Key().ScriptPubKey;
				var address2 = new Key().ScriptPubKey;
				var address3 = new Key().ScriptPubKey;

				var h = new Transaction().GetHash();
				tracker.AddressCreated(1, TransactionType.ClientEscrow, address, CorrelationId.Zero);
				tracker.AddressCreated(1, TransactionType.ClientFulfill, address2, CorrelationId.Zero);
				tracker.TransactionCreated(1, TransactionType.ClientEscrow, h, CorrelationId.Zero);

				Assert.Equal(0, tracker.GetRecords(2).Length);
				Assert.Equal(3, tracker.GetRecords(1).Length);

				Assert.NotNull(tracker.Search(address));
				Assert.True(tracker.Search(address3).Length == 0);
			}
		}

		[Fact]
		public void CanCompleteCycleWithMachineState()
		{
			CanCompleteCycleWithMachineStateCore(true, true);
			CanCompleteCycleWithMachineStateCore(true, false);
			CanCompleteCycleWithMachineStateCore(false, true);
			CanCompleteCycleWithMachineStateCore(false, false);
		}

		[Fact]
		public void CanParseAndGenerateTBAddresses()
		{
			Assert.Equal("ctb://ye33yfa66xpqsjdu.onion?h=2fc0fba4f88fae783dd6e8f972920d51586e3084",
				new TumblerUrlBuilder("ctb://ye33yfa66xpqsjdu.onion?h=2fc0fba4f88fae783dd6e8f972920d51586e3084").ToString());
			Assert.Equal("ctb://ye33yfa66xpqsjdu.onion?h=2fc0fba4f88fae783dd6e8f972920d51586e3084",
				new TumblerUrlBuilder("ctb://ye33yfa66xpqsjdu.onion/?h=2fc0fba4f88fae783dd6e8f972920d51586e3084").ToString());

			Assert.Throws<FormatException>(() => new TumblerUrlBuilder("ctb://ye33yfa66xpqsjdu.onio?h=2fc0fba4f88fae783dd6e8f972920d51586e3084"));
			Assert.Throws<FormatException>(() => new TumblerUrlBuilder("ctb://ye33yfa66xpqsjdu.onion/h=2fc0fba4f88fae783dd6e8f972920d51586e3084"));
			Assert.Throws<FormatException>(() => new TumblerUrlBuilder("ctb://ye33yfa66xpqsjdu.onion/?q=2fc0fba4f88fae783dd6e8f972920d51586e3084"));
			Assert.Throws<FormatException>(() => new TumblerUrlBuilder("ctb://ye33yfa66xpqsjdu.onion/"));
		}

		[Fact]
		public void CanGenerateAddress()
		{
			using(var server = TumblerServerTester.Create())
			{
				var key = new ExtKey().GetWif(Network.RegTest);
				var w = new ClientDestinationWallet(key.Neuter(), new KeyPath("0/1"), server.ClientRuntime.Repository, server.ClientRuntime.Network);

				var k1 = w.GetNewDestination();
				var k2 = w.GetNewDestination();
				Assert.Equal(new KeyPath("0/1/0"), w.GetKeyPath(k1));
				Assert.Equal(new KeyPath("0/1/1"), w.GetKeyPath(k2));
				Assert.Null(w.GetKeyPath(new Key().ScriptPubKey));

				var wrpc = new RPCDestinationWallet(server.AliceNode.CreateRPCClient());

				k1 = wrpc.GetNewDestination();
				k2 = wrpc.GetNewDestination();

				// The setup give an address to Alice
				Assert.Equal(new KeyPath("0'/0'/2'"), wrpc.GetKeyPath(k1));
				Assert.Equal(new KeyPath("0'/0'/3'"), wrpc.GetKeyPath(k2));
				Assert.Null(w.GetKeyPath(new Key().ScriptPubKey));
			}
		}

		public void CanCompleteCycleWithMachineStateCore(bool cooperativeClient, bool cooperativeTumbler)
		{
			using(var server = TumblerServerTester.Create())
			{
				server.ServerRuntime.Cooperative = cooperativeTumbler;
				server.ClientRuntime.Cooperative = cooperativeClient;

				var machine = server.CreateStateMachine();

				var serverTracker = server.ServerRuntime.Tracker;
				var clientTracker = machine.Tracker;


				machine.Update();
				var cycle = machine.ClientChannelNegotiation.GetCycle();

				server.MineTo(server.AliceNode, cycle, CyclePhase.ClientChannelEstablishment);

				var alice = machine.Runtime.CreateTumblerClient(machine.StartCycle, Identity.Alice);

				var escrow1 = alice.RequestTumblerEscrowKey();
				var escrow2 = alice.RequestTumblerEscrowKey();
				Assert.Equal(0, escrow1.KeyIndex);
				Assert.Equal(1, escrow2.KeyIndex);
				machine.Update();

				Assert.NotEqual(uint160.Zero, machine.SolverClientSession.Id);
				Assert.NotNull(machine.SolverClientSession.Id);
				clientTracker.AssertKnown(TransactionType.ClientEscrow, machine.SolverClientSession.EscrowedCoin.ScriptPubKey);
				clientTracker.AssertKnown(TransactionType.ClientEscrow, machine.SolverClientSession.EscrowedCoin.Outpoint.Hash);

				//Wait the client escrow is confirmed
				server.AliceNode.FindBlock(2);
				server.SyncNodes();

				//Server does not track anything until Alice gives proof of the escrow
				Assert.Equal(0, server.ServerRuntime.Services.BlockExplorerService
						.GetTransactionsAsync(machine.SolverClientSession.EscrowedCoin.ScriptPubKey, false)
						.GetAwaiter().GetResult()
						.Count());
				serverTracker.AssertNotKnown(machine.SolverClientSession.EscrowedCoin.ScriptPubKey);

				machine.Update();


				//Server is now tracking Alice's escrow
				Assert.Equal(1, server.ServerRuntime.Services.BlockExplorerService
						.GetTransactionsAsync(machine.SolverClientSession.EscrowedCoin.ScriptPubKey, false)
						.GetAwaiter().GetResult()
						.Count());
				serverTracker.AssertKnown(TransactionType.ClientEscrow, machine.SolverClientSession.EscrowedCoin.ScriptPubKey);
				serverTracker.AssertKnown(TransactionType.ClientEscrow, machine.SolverClientSession.EscrowedCoin.Outpoint.Hash);
				//


				server.MineTo(server.AliceNode, cycle, CyclePhase.TumblerChannelEstablishment);
				machine.Update();

				Assert.Equal(PaymentStateMachineStatus.TumblerChannelCreating, machine.Status);
				//Wait escrow broadcasted
				Thread.Sleep(1000);
				server.TumblerNode.Generate(1);
				machine.Update();
				Assert.Equal(PaymentStateMachineStatus.TumblerChannelCreated, machine.Status);

				Assert.NotEqual(uint160.Zero, machine.PromiseClientSession.Id);
				Assert.NotEqual(machine.SolverClientSession.Id, machine.PromiseClientSession.Id);
				Assert.NotNull(machine.PromiseClientSession.Id);
				serverTracker.AssertKnown(TransactionType.TumblerEscrow, machine.PromiseClientSession.EscrowedCoin.ScriptPubKey);
				serverTracker.AssertKnown(TransactionType.TumblerEscrow, machine.PromiseClientSession.EscrowedCoin.Outpoint.Hash);
				clientTracker.AssertKnown(TransactionType.TumblerEscrow, machine.PromiseClientSession.EscrowedCoin.ScriptPubKey);
				clientTracker.AssertKnown(TransactionType.TumblerEscrow, machine.PromiseClientSession.EscrowedCoin.Outpoint.Hash);

				server.MineTo(server.TumblerNode, cycle, CyclePhase.PaymentPhase);
				machine.Update();

				//Wait escape transaction to be broadcasted
				Thread.Sleep(1000);
				Block block = server.TumblerNode.FindBlock(1).First();

				if(cooperativeClient && cooperativeTumbler)
				{
					Assert.Equal(PaymentStateMachineStatus.PuzzleSolutionObtained, machine.Status);
					//Escape should be mined
					Assert.Equal(2, block.Transactions.Count);

					serverTracker.AssertKnown(TransactionType.ClientEscape, block.Transactions[1].GetHash());
					serverTracker.AssertKnown(TransactionType.ClientEscape, block.Transactions[1].Outputs[0].ScriptPubKey);
				}
				else
				{
					if(!cooperativeTumbler)
						Assert.Equal(PaymentStateMachineStatus.UncooperativeTumbler, machine.Status);
					Assert.Equal(1, block.Transactions.Count);
				}

				server.MineTo(server.TumblerNode, cycle, CyclePhase.PaymentPhase, true);

				Transaction[] transactions = null;
				if(!cooperativeClient || !cooperativeTumbler)
				{
					//Offer + Fulfill should be broadcasted
					transactions = server.ServerRuntime.Services.TrustedBroadcastService.TryBroadcast();
					Assert.Equal(2, transactions.Length);

					//Sanity check if trusted broadcaster know about this
					var knownTx = server.ServerRuntime.Services.TrustedBroadcastService.GetKnownTransaction(transactions[0].GetHash());
					Assert.NotNull(knownTx);
					Assert.Equal(transactions[0].GetHash(), knownTx.Transaction.GetHash());

					var unmalleatedTransactions = transactions;

					serverTracker.AssertKnown(TransactionType.ClientOffer, transactions[0].GetHash());
					serverTracker.AssertKnown(TransactionType.ClientFulfill, transactions[1].GetHash());
				}

				server.MineTo(server.TumblerNode, cycle, CyclePhase.ClientCashoutPhase);

				machine.Update();

				if(cooperativeTumbler)
					//Received the solution out of blockchain, so the transaction should have been planned in advance
					transactions = server.ClientRuntime.Services.TrustedBroadcastService.TryBroadcast();
				else
					//Received the solution from the blockchain, the transaction has not been planned in advance
					transactions = server.ClientRuntime.Services.BroadcastService.TryBroadcast();
				Assert.Equal(1, transactions.Length);
				block = server.AliceNode.FindBlock().First();
				//Should contains TumblerCashout
				Assert.Equal(2, block.Transactions.Count);

				clientTracker.AssertKnown(TransactionType.TumblerCashout, block.Transactions[1].GetHash());
				clientTracker.AssertKnown(TransactionType.TumblerCashout, block.Transactions[1].Outputs[0].ScriptPubKey);

				//Just a sanity tests, this one contains escrow redeem and offer redeem, both of which should not be available now
				transactions = server.ClientRuntime.Services.BroadcastService.TryBroadcast();
				Assert.Equal(0, transactions.Length);


				var allTransactions = server.AliceNode.CreateNodeClient().GetBlocks().SelectMany(b => b.Transactions).ToDictionary(t => t.GetHash());
				var expectedRate = new FeeRate(100, 1);

				foreach(var txId in new[]
				{
					TransactionType.ClientEscape,
					TransactionType.TumblerEscrow,
					TransactionType.TumblerRedeem,
					TransactionType.ClientOffer,
					TransactionType.ClientFulfill,
				}.SelectMany(r => serverTracker.GetRecords(cycle.Start).Where(t => t.RecordType == RecordType.Transaction && t.TransactionType == r)))
				{
					if(txId != null)
					{
						var tx = allTransactions.TryGet(txId.TransactionId) ??
							server.ServerRuntime.Services.TrustedBroadcastService.GetKnownTransaction(txId.TransactionId)?.Transaction ?? server.ServerRuntime.Services.BroadcastService.GetKnownTransaction(txId.TransactionId);
						if(tx != null)
							AssertRate(allTransactions, expectedRate, tx);
					}
				}


				expectedRate = new FeeRate(50, 1);
				foreach(var txId in new[]
				{
					TransactionType.ClientEscrow,
					TransactionType.ClientRedeem,
					TransactionType.ClientOfferRedeem,
					TransactionType.TumblerCashout
				}.SelectMany(r => clientTracker.GetRecords(cycle.Start).Where(t => t.RecordType == RecordType.Transaction && t.TransactionType == r)))
				{
					if(txId != null)
					{
						var tx = allTransactions.TryGet(txId.TransactionId) ??
							server.ClientRuntime.Services.TrustedBroadcastService.GetKnownTransaction(txId.TransactionId)?.Transaction ?? server.ClientRuntime.Services.BroadcastService.GetKnownTransaction(txId.TransactionId);
						if(tx != null
							 && tx.Inputs[0].PrevOut.Hash != uint256.Zero) //Client offer redeem, as it is broadcasted to the trusted broadcaster before offer is known
							AssertRate(allTransactions, expectedRate, tx);
					}
				}
			}
		}

		private static void AssertRate(Dictionary<uint256, Transaction> allTransactions, FeeRate expectedRate, Transaction tx)
		{
			var previousCoins = tx.Inputs.Select(t => t.PrevOut).Select(t => allTransactions[t.Hash].Outputs.AsCoins().ToArray()[(int)t.N]).ToArray();
			var rate = tx.GetFeeRate(previousCoins);
			var tb = new TransactionBuilder();
			tb.StandardTransactionPolicy.CheckFee = false;
			tb.AddCoins(previousCoins);
			Assert.True(tb.Verify(tx));
			Assert.True(expectedRate.FeePerK.Almost(rate.FeePerK, 0.05m));
		}


		[Fact]
		public void EscrowGetRedeemedIfTimeout()
		{
			EscrowGetRedeemedIfTimeoutCore(false);
			EscrowGetRedeemedIfTimeoutCore(true);
		}

		void EscrowGetRedeemedIfTimeoutCore(bool fulfill)
		{
			using(var server = TumblerServerTester.Create())
			{
				var machine = server.CreateStateMachine();
				machine.Update();
				var cycle = machine.ClientChannelNegotiation.GetCycle();

				server.MineTo(server.AliceNode, cycle, CyclePhase.ClientChannelEstablishment);


				machine.Update();

				//Wait the client escrow is confirmed
				server.AliceNode.FindBlock(2);
				server.SyncNodes();

				//Server does not track anything until Alice gives proof of the escrow
				Assert.Equal(0, server.ServerRuntime.Services.BlockExplorerService
						.GetTransactionsAsync(machine.SolverClientSession.EscrowedCoin.ScriptPubKey, false)
						.GetAwaiter().GetResult()
						.Count());

				machine.Update();

				//Server is now tracking Alice's escrow
				Assert.Equal(1, server.ServerRuntime.Services.BlockExplorerService
						.GetTransactionsAsync(machine.SolverClientSession.EscrowedCoin.ScriptPubKey, false)
						.GetAwaiter().GetResult()
						.Count());

				server.MineTo(server.AliceNode, cycle, CyclePhase.TumblerChannelEstablishment);
				machine.Update();

				Assert.Equal(PaymentStateMachineStatus.TumblerChannelCreating, machine.Status);

				//Wait escrow broadcasted
				Thread.Sleep(1000);

				//Make sure the tumbler escrow is broadcasted an mined
				var broadcasted = server.ServerRuntime.Services.BroadcastService.TryBroadcast();
				Assert.Equal(1, broadcasted.Length);
				server.ServerRuntime.Tracker.AssertKnown(TransactionType.TumblerEscrow, broadcasted[0].GetHash());

				machine.Update();
				Assert.Equal(PaymentStateMachineStatus.TumblerChannelCreated, machine.Status);

				server.MineTo(server.TumblerNode, cycle, CyclePhase.TumblerChannelEstablishment, end: true, offset: -1);
				machine.Update();

				if(!fulfill)
				{
					//Client Refund broadcasted exactly when we are at ClientCashoutPhase + SafetyPeriodDuration
					server.MineTo(server.AliceNode, cycle, CyclePhase.ClientCashoutPhase, offset: cycle.SafetyPeriodDuration - 1);
					broadcasted = server.ClientRuntime.Services.TrustedBroadcastService.TryBroadcast();
					Assert.Equal(0, broadcasted.Length);
					server.MineTo(server.AliceNode, cycle, CyclePhase.ClientCashoutPhase, offset: cycle.SafetyPeriodDuration);
					broadcasted = server.ClientRuntime.Services.TrustedBroadcastService.TryBroadcast();
					Assert.Equal(1, broadcasted.Length);
					server.ClientRuntime.Tracker.AssertKnown(TransactionType.ClientRedeem, broadcasted[0].GetHash());
					////////////////////////////////////////////////////////////////////////////////

					//Tumbler Refund broadcasted exactly when we are at end of ClientCashoutPhase + SafetyPeriodDuration
					server.MineTo(server.AliceNode, cycle, CyclePhase.ClientCashoutPhase, end: true, offset: cycle.SafetyPeriodDuration - 1);
					broadcasted = server.ServerRuntime.Services.TrustedBroadcastService.TryBroadcast();
					Assert.Equal(0, broadcasted.Length);
					server.MineTo(server.AliceNode, cycle, CyclePhase.ClientCashoutPhase, end: true, offset: cycle.SafetyPeriodDuration);
					broadcasted = server.ServerRuntime.Services.TrustedBroadcastService.TryBroadcast();
					Assert.Equal(1, broadcasted.Length);
					server.ServerRuntime.Tracker.AssertKnown(TransactionType.TumblerRedeem, broadcasted[0].GetHash());
					////////////////////////////////////////////////////////////////////////////////
				}
				else
				{
					server.ServerRuntime.Cooperative = false;
					server.ServerRuntime.NoFulFill = true;
					server.ClientRuntime.Cooperative = false;

					//The tumbler plan offer for end of payment period, but not the fulfill
					server.MineTo(server.AliceNode, cycle, CyclePhase.PaymentPhase);
					machine.Update();

					//The tumbler should now broadcast the offer, but not the fulfill
					server.MineTo(server.TumblerNode, cycle, CyclePhase.PaymentPhase, true);
					broadcasted = server.ServerRuntime.Services.TrustedBroadcastService.TryBroadcast();
					Assert.Equal(1, broadcasted.Length);
					server.ServerRuntime.Tracker.AssertKnown(TransactionType.ClientOffer, broadcasted[0].GetHash());
					server.TumblerNode.FindBlock(1);


					//Client Offer Refund broadcasted exactly when we are at ClientCashoutPhase + SafetyPeriodDuration
					server.MineTo(server.AliceNode, cycle, CyclePhase.ClientCashoutPhase, offset: cycle.SafetyPeriodDuration - 1);
					broadcasted = server.ClientRuntime.Services.TrustedBroadcastService.TryBroadcast();
					Assert.Equal(0, broadcasted.Length);
					server.MineTo(server.AliceNode, cycle, CyclePhase.ClientCashoutPhase, offset: cycle.SafetyPeriodDuration);
					broadcasted = server.ClientRuntime.Services.TrustedBroadcastService.TryBroadcast();
					Assert.Equal(1, broadcasted.Length);
					server.ClientRuntime.Tracker.AssertKnown(TransactionType.ClientOfferRedeem, broadcasted[0].GetHash());
					////////////////////////////////////////////////////////////////////////////////
				}
			}
		}
	}
}
