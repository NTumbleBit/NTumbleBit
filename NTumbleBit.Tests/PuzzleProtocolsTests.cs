﻿using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Policy;
using Newtonsoft.Json;
using NTumbleBit.BouncyCastle.Crypto.Parameters;
using NTumbleBit.BouncyCastle.Math;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NTumbleBit.Tests
{
	public class PuzzleProtocolsTests
	{

		[Fact]
		public void CanGenerateParseAndSaveRsaKey()
		{
			RsaKey key = new RsaKey();
			RsaKey key2 = new RsaKey(key.ToBytes());
			Assert.True(key.ToBytes().SequenceEqual(key2.ToBytes()));
			Assert.True(key.PubKey.ToBytes().SequenceEqual(key2.PubKey.ToBytes()));
			Assert.True(new RsaPubKey(key.PubKey.ToBytes()).ToBytes().SequenceEqual(key2.PubKey.ToBytes()));
			Assert.Throws<FormatException>(() => new RsaKey(new byte[1]));
		}

		[Fact]
		public void CanSolvePuzzle()
		{
			RsaKey key = TestKeys.Default;
			PuzzleSolution solution = null;
			var puzzle = key.PubKey.GeneratePuzzle(ref solution);
			PuzzleSolution solution2 = puzzle.Solve(key);
			Assert.True(puzzle.Verify(solution));
			Assert.True(solution == solution2);


			puzzle = key.PubKey.GeneratePuzzle(ref solution);
			BlindFactor blind = null;
			Puzzle blindedPuzzle = puzzle.Blind(ref blind);
			PuzzleSolution blindedSolution = key.SolvePuzzle(blindedPuzzle);
			var unblinded = blindedSolution.Unblind(key.PubKey, blind);

			Assert.True(unblinded == solution);
		}


		[Fact]
		public void CanSignAndVerify()
		{
			RsaKey key = TestKeys.Default;
			for(int i = 0; i < 100; i++)
			{
				var data = RandomUtils.GetBytes(234);
				uint160 nonce;
				var sig = key.Sign(data, out nonce);
				Assert.True(key.PubKey.Verify(sig, data, nonce));
			}


		}

		static byte[] GenerateEncryptableData(RsaKeyParameters key)
		{
			while(true)
			{
				var bytes = RandomUtils.GetBytes(RsaKey.KeySize / 8);
				BigInteger input = new BigInteger(1, bytes);
				if(input.CompareTo(key.Modulus) >= 0)
					continue;
				return bytes;
			}
		}

		[Fact]
		//https://medium.com/@nicolasdorier/tumblebit-tumbler-mode-ea44e9a2a2ec#.a4wgwa86u
		public void CanCalculatePhase()
		{
			var parameter = new CycleParameters()
			{
				Start = 100,
				RegistrationDuration = 10,
				ClientChannelEstablishmentDuration = 11,
				TumblerChannelEstablishmentDuration = 12,
				TumblerCashoutDuration = 10,
				ClientCashoutDuration = 13,
				PaymentPhaseDuration = 3,
				SafetyPeriodDuration = 2,
			};

			Assert.True(parameter.IsInPhase(CyclePhase.Registration, 100));
			Assert.True(parameter.IsInPhase(CyclePhase.Registration, 109));
			Assert.False(parameter.IsInPhase(CyclePhase.Registration, 110));

			Assert.True(parameter.IsInPhase(CyclePhase.ClientChannelEstablishment, 110));
			Assert.True(parameter.IsInPhase(CyclePhase.ClientChannelEstablishment, 120));
			Assert.False(parameter.IsInPhase(CyclePhase.ClientChannelEstablishment, 121));

			Assert.True(parameter.IsInPhase(CyclePhase.TumblerChannelEstablishment, 121));
			Assert.True(parameter.IsInPhase(CyclePhase.TumblerChannelEstablishment, 132));
			Assert.False(parameter.IsInPhase(CyclePhase.TumblerChannelEstablishment, 133));

			Assert.False(parameter.IsInPhase(CyclePhase.TumblerCashoutPhase, 133));
			Assert.False(parameter.IsInPhase(CyclePhase.TumblerCashoutPhase, 134));
			Assert.True(parameter.IsInPhase(CyclePhase.TumblerCashoutPhase, 135));
			Assert.True(parameter.IsInPhase(CyclePhase.TumblerCashoutPhase, 144));
			Assert.False(parameter.IsInPhase(CyclePhase.TumblerCashoutPhase, 145));

			Assert.False(parameter.IsInPhase(CyclePhase.PaymentPhase, 133));
			Assert.False(parameter.IsInPhase(CyclePhase.PaymentPhase, 134));
			Assert.True(parameter.IsInPhase(CyclePhase.PaymentPhase, 135));
			Assert.True(parameter.IsInPhase(CyclePhase.PaymentPhase, 137));
			Assert.False(parameter.IsInPhase(CyclePhase.PaymentPhase, 138));

			Assert.True(parameter.IsInPhase(CyclePhase.ClientCashoutPhase, 145));
			Assert.True(parameter.IsInPhase(CyclePhase.ClientCashoutPhase, 157));
			Assert.False(parameter.IsInPhase(CyclePhase.ClientCashoutPhase, 158));

			//At block 147 we will be able to broadcast refund for expected confirmation at 148
			Assert.Equal(147, parameter.GetClientLockTime().Height);


			var total = parameter.GetPeriods().Total;
			Assert.Equal(100, total.Start);
			Assert.Equal(160, total.End);

			////At block 160 we will be able to broadcast refund for expected confirmation at 161
			Assert.Equal(160, parameter.GetTumblerLockTime().Height);

			var cycleGenerator = new OverlappedCycleGenerator();
			cycleGenerator.FirstCycle = parameter;
			cycleGenerator.RegistrationOverlap = 3;
			Assert.Equal(100, cycleGenerator.GetRegistratingCycle(100).Start);
			Assert.Equal(100, cycleGenerator.GetRegistratingCycle(106).Start);
			Assert.Equal(107, cycleGenerator.GetRegistratingCycle(107).Start);
		}

		[Fact]
		public void TestChacha()
		{
			byte[] msg = Encoding.UTF8.GetBytes("123123123123123123123123123123");
			var key1 = Encoding.ASCII.GetBytes("xxxxxxxxxxxxxxxx");
			var iv1 = Encoding.ASCII.GetBytes("aaaaaaaa");
			var encrypted = Utils.ChachaEncrypt(msg, ref key1, ref iv1);
			Assert.False(encrypted.SequenceEqual(msg));
			var decrypted = Utils.ChachaDecrypt(encrypted, key1);
			Assert.True(decrypted.SequenceEqual(msg));
		}

		FeeRate FeeRate = new FeeRate(Money.Satoshis(50), 1);

		[Fact]
		public void TestPuzzlePromise()
		{
			RsaKey key = TestKeys.Default;

			Key serverEscrow = new Key();
			Key serverRedeem = new Key();
			Key clientEscrow = new Key();

			var parameters = new PromiseParameters(key.PubKey)
			{
				FakeTransactionCount = 5,
				RealTransactionCount = 5
			};

			var client = new PromiseClientSession(parameters);
			var server = new PromiseServerSession(parameters);

			var coin = CreateEscrowCoin(serverEscrow.PubKey, clientEscrow.PubKey, serverRedeem.PubKey);

			client.ConfigureEscrowedCoin(coin, clientEscrow);
			SignaturesRequest request = client.CreateSignatureRequest(clientEscrow.PubKey.Hash, FeeRate);
			RoundTrip(ref client, parameters);
			RoundTrip(ref request);

			server.ConfigureEscrowedCoin(coin, serverEscrow, serverRedeem);
			PuzzlePromise.ServerCommitment[] commitments = server.SignHashes(request);
			RoundTrip(ref server, parameters);
			RoundTrip(ref commitments);

			PuzzlePromise.ClientRevelation revelation = client.Reveal(commitments);
			RoundTrip(ref client, parameters);
			RoundTrip(ref revelation);

			ServerCommitmentsProof proof = server.CheckRevelation(revelation);
			RoundTrip(ref server, parameters);
			RoundTrip(ref proof);

			var puzzleToSolve = client.CheckCommitmentProof(proof);
			RoundTrip(ref client, parameters);
			Assert.NotNull(puzzleToSolve);

			var solution = key.SolvePuzzle(puzzleToSolve);
			var transactions = client.GetSignedTransactions(solution).ToArray();
			RoundTrip(ref client, parameters);
			Assert.True(transactions.Length == parameters.RealTransactionCount);


			// In case things do not go well and timeout is hit...
			var redeemTransaction = server.CreateRedeemTransaction(FeeRate, new Key().ScriptPubKey);
			TransactionBuilder bb = new TransactionBuilder();
			bb.AddCoins(server.GetInternalState().EscrowedCoin);
			Assert.True(bb.Verify(redeemTransaction.Transaction));

			//Check can ve reclaimed if malleated
			bb = new TransactionBuilder();
			var escrow = server.GetInternalState().EscrowedCoin;
			escrow.Outpoint = new OutPoint(escrow.Outpoint.Hash, 10);
			bb.AddCoins(escrow);
			var resigned = redeemTransaction.ReSign(escrow);
			Assert.False(bb.Verify(redeemTransaction.Transaction));
			Assert.True(bb.Verify(resigned));

			foreach(var tx in transactions)
			{
				TransactionBuilder builder = new TransactionBuilder();
				builder.Extensions.Add(new EscrowBuilderExtension());
				builder.AddCoins(coin);
				Assert.True(builder.Verify(tx));
			}
		}

		private ScriptCoin CreateEscrowCoin(PubKey escrow1, PubKey escrow2, PubKey redeemKey)
		{
			var redeem = EscrowScriptBuilder.CreateEscrow(new[] { escrow1, escrow2 }, redeemKey, new LockTime(0));
			var scriptCoin = new Coin(new OutPoint(new uint256(RandomUtils.GetBytes(32)), 0),
				new TxOut()
				{
					Value = Money.Coins(1.5m),
					ScriptPubKey = redeem.Hash.ScriptPubKey
				}).ToScriptCoin(redeem);
			return scriptCoin;
		}

		[Fact]
		public void TestPuzzleSolver()
		{
			RsaKey key = TestKeys.Default;
			PuzzleSolution expectedSolution = null;
			Puzzle puzzle = key.PubKey.GeneratePuzzle(ref expectedSolution);

			var parameters = new SolverParameters()
			{
				FakePuzzleCount = 50,
				RealPuzzleCount = 10,
				ServerKey = key.PubKey
			};
			SolverClientSession client = new SolverClientSession(parameters);
			SolverServerSession server = new SolverServerSession(key, parameters);

			var clientEscrow = new Key();
			var serverEscrow = new Key();
			var clientRedeem = new Key();

			var escrow = CreateEscrowCoin(clientEscrow.PubKey, serverEscrow.PubKey, clientRedeem.PubKey);
			client.ConfigureEscrowedCoin(escrow, clientEscrow, clientRedeem);
			client.AcceptPuzzle(puzzle.PuzzleValue);
			RoundTrip(ref client, parameters);
			PuzzleValue[] puzzles = client.GeneratePuzzles();
			RoundTrip(ref client, parameters);
			RoundTrip(ref puzzles);

			server.ConfigureEscrowedCoin(escrow, serverEscrow);
			var commitments = server.SolvePuzzles(puzzles);
			RoundTrip(ref server, parameters, key);
			RoundTrip(ref commitments);

			var revelation = client.Reveal(commitments);
			RoundTrip(ref client, parameters);
			RoundTrip(ref revelation);

			SolutionKey[] fakePuzzleKeys = server.CheckRevelation(revelation);
			RoundTrip(ref server, parameters, key);
			RoundTrip(ref fakePuzzleKeys);


			BlindFactor[] blindFactors = client.GetBlindFactors(fakePuzzleKeys);
			RoundTrip(ref client, parameters);
			RoundTrip(ref blindFactors);

			var offerInformation = server.CheckBlindedFactors(blindFactors, FeeRate);
			RoundTrip(ref server, parameters, key);

			var clientOfferSig = client.SignOffer(offerInformation);

			//Verify if the scripts are correctly created
			var fulfill = server.fulfillOffer(clientOfferSig, new Key().ScriptPubKey, FeeRate);

			var offerTransaction = server.GetSignedOfferTransaction();
			TransactionBuilder txBuilder = new TransactionBuilder();
			txBuilder.AddCoins(client.EscrowedCoin);
			Assert.True(txBuilder.Verify(offerTransaction.Transaction));

			txBuilder = new TransactionBuilder();
			txBuilder.AddCoins(offerTransaction.Transaction.Outputs.AsCoins().ToArray());
			Assert.True(txBuilder.Verify(fulfill.Transaction));

			//Check if can resign fulfill in case offer get malleated
			offerTransaction.Transaction.LockTime = new LockTime(1);
			fulfill.Transaction.Inputs[0].PrevOut = offerTransaction.Transaction.Outputs.AsCoins().First().Outpoint;
			txBuilder = new TransactionBuilder();
			txBuilder.Extensions.Add(new OfferBuilderExtension());
			txBuilder.AddKeys(server.GetInternalState().fulfillKey);
			txBuilder.AddCoins(offerTransaction.Transaction.Outputs.AsCoins().ToArray());
			txBuilder.SignTransactionInPlace(fulfill.Transaction);
			Assert.True(txBuilder.Verify(fulfill.Transaction));
			////////////////////////////////////////////////

			client.CheckSolutions(fulfill.Transaction);
			RoundTrip(ref client, parameters);
			var solution = client.GetSolution();
			RoundTrip(ref client, parameters);
			Assert.True(solution == expectedSolution);
		}

		private void RoundtripJson<T>(ref T result)
		{
			JsonSerializerSettings settings = new JsonSerializerSettings();
			Serializer.RegisterFrontConverters(settings);
			var str = JsonConvert.SerializeObject(result, settings);
			result = JsonConvert.DeserializeObject<T>(str, settings);
		}

		private void RoundTrip<T>(ref T commitments)
		{
			RoundtripJson(ref commitments);
		}

		private void RoundTrip(ref SolverServerSession server, SolverParameters parameters, RsaKey key)
		{
			var clone = Serializer.Clone(server.GetInternalState());
			server = new SolverServerSession(key, parameters, clone);
		}

		private void RoundTrip(ref SolverClientSession client, SolverParameters parameters)
		{
			var clone = Serializer.Clone(client.GetInternalState());
			client = new SolverClientSession(parameters, clone);
		}

		private void RoundTrip(ref PromiseServerSession server, PromiseParameters parameters)
		{
			var clone = Serializer.Clone(server.GetInternalState());
			server = new PromiseServerSession(clone, parameters);
		}

		private void RoundTrip(ref PromiseClientSession client, PromiseParameters parameters)
		{
			var clone = Serializer.Clone(client.GetInternalState());
			client = new PromiseClientSession(parameters, clone);
		}

		LockTime EscrowDate = new LockTime(new DateTimeOffset(1988, 07, 18, 0, 0, 0, TimeSpan.Zero));
		Money Amount = Money.Coins(1.0m);

		[Fact]
		public void CanBlind()
		{
			RsaKey key = TestKeys.Default;

			PuzzleSolution solution = null;
			BlindFactor blind = null;

			Puzzle puzzle = key.PubKey.GeneratePuzzle(ref solution);
			Puzzle blindedPuzzle = puzzle.Blind(ref blind);
			Assert.True(puzzle != blindedPuzzle);
			Assert.True(puzzle == blindedPuzzle.Unblind(blind));


			PuzzleSolution blindedSolution = blindedPuzzle.Solve(key);
			Assert.False(puzzle.Verify(blindedSolution));
			Assert.True(puzzle.Verify(blindedSolution.Unblind(key.PubKey, blind)));
		}
	}
}
