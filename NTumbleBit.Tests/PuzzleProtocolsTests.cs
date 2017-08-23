using NBitcoin;
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

		private static byte[] GenerateEncryptableData(RsaKeyParameters key)
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
		public void CanCalculateStandardPhases()
		{
			StandardCycles cycles = new StandardCycles(Network.Main.Consensus, true);
			Assert.NotNull(cycles.GetStandardCycle("shorty"));

			cycles = new StandardCycles(Network.Main.Consensus, false);
			Assert.Null(cycles.GetStandardCycle("shorty"));

			var kotori = cycles.GetStandardCycle("kotori");
			Assert.Equal(Money.Coins(1), kotori.Denomination);
			Assert.Equal(TimeSpan.FromHours(4), kotori.GetLength(false));
			Assert.Equal(TimeSpan.FromHours(19.5), kotori.GetLength(true));
			Assert.Equal(Money.Coins(6), kotori.CoinsPerDay());
		}

		[Fact]
		//https://medium.com/@nicolasdorier/tumblebit-tumbler-mode-ea44e9a2a2ec#.a4wgwa86u
		public void CanCalculatePhase()
		{
			var parameter = new CycleParameters
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

			Assert.Equal("{[..o.......]..[...........]..[............]..[[...].......][.............]..}", parameter.ToString(102));
			//              0          10 12          23 25           37  39  42       49            62

			Assert.True(parameter.IsInPhase(CyclePhase.Registration, 100));
			Assert.True(parameter.IsInPhase(CyclePhase.Registration, 109));
			Assert.False(parameter.IsInPhase(CyclePhase.Registration, 110));

			Assert.True(parameter.IsInPhase(CyclePhase.ClientChannelEstablishment, 112));

			Assert.True(parameter.IsInPhase(CyclePhase.TumblerChannelEstablishment, 125));

			Assert.True(parameter.IsInPhase(CyclePhase.TumblerCashoutPhase, 139));

			Assert.True(parameter.IsInPhase(CyclePhase.PaymentPhase, 139));

			Assert.True(parameter.IsInPhase(CyclePhase.ClientCashoutPhase, 149));

			Assert.Equal(149 + 2, parameter.GetClientLockTime().Height);


			var total = parameter.GetPeriods().Total;
			Assert.Equal(100, total.Start);
			Assert.Equal(162 + 2, total.End);

			Assert.Equal(162 + 2, parameter.GetTumblerLockTime().Height);

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

		private FeeRate FeeRate = new FeeRate(Money.Satoshis(50), 1);

		[Fact]
		public void TestPuzzlePromise()
		{
			RsaKey key = TestKeys.Default;

			Key serverEscrow = new Key();
			Key clientEscrow = new Key();

			var parameters = new PromiseParameters(key.PubKey)
			{
				FakeTransactionCount = 5,
				RealTransactionCount = 5
			};

			var client = new PromiseClientSession(parameters);
			var server = new PromiseServerSession(parameters);

			var coin = CreateEscrowCoin(serverEscrow.PubKey, clientEscrow.PubKey);

			client.ConfigureEscrowedCoin(coin, clientEscrow);
			SignaturesRequest request = client.CreateSignatureRequest(clientEscrow.PubKey.Hash, FeeRate);
			RoundTrip(ref client, parameters);
			RoundTrip(ref request);

			server.ConfigureEscrowedCoin(uint160.Zero, coin, serverEscrow, new Key().ScriptPubKey);
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


			var escrow = server.GetInternalState().EscrowedCoin;
			// In case things do not go well and timeout is hit...
			var redeemTransaction = server.CreateRedeemTransaction(FeeRate);
			var resigned = redeemTransaction.ReSign(escrow);
			TransactionBuilder bb = new TransactionBuilder();
			bb.AddCoins(server.GetInternalState().EscrowedCoin);
			Assert.True(bb.Verify(resigned));

			//Check can ve reclaimed if malleated
			bb = new TransactionBuilder();
			escrow.Outpoint = new OutPoint(escrow.Outpoint.Hash, 10);
			bb.AddCoins(escrow);
			resigned = redeemTransaction.ReSign(escrow);
			Assert.False(bb.Verify(redeemTransaction.Transaction));
			Assert.True(bb.Verify(resigned));
		}

		private ScriptCoin CreateEscrowCoin(PubKey initiator, PubKey receiver)
		{
			var redeem = new EscrowScriptPubKeyParameters(initiator, receiver, new LockTime(10)).ToScript();
			var scriptCoin = new Coin(new OutPoint(new uint256(RandomUtils.GetBytes(32)), 0),
				new TxOut
				{
					Value = Money.Coins(1.5m),
					ScriptPubKey = redeem.WitHash.ScriptPubKey.Hash.ScriptPubKey
				}).ToScriptCoin(redeem);
			return scriptCoin;
		}

		[Fact]
		public void TestPuzzleSolver()
		{
			RsaKey key = TestKeys.Default;
			PuzzleSolution expectedSolution = null;
			Puzzle puzzle = key.PubKey.GeneratePuzzle(ref expectedSolution);

			var parameters = new SolverParameters
			{
				FakePuzzleCount = 50,
				RealPuzzleCount = 10,
				ServerKey = key.PubKey
			};
			SolverClientSession client = new SolverClientSession(parameters);
			SolverServerSession server = new SolverServerSession(key, parameters);

			var clientEscrow = new Key();
			var serverEscrow = new Key();

			var escrow = CreateEscrowCoin(clientEscrow.PubKey, serverEscrow.PubKey);
			var redeemDestination = new Key().ScriptPubKey;
			client.ConfigureEscrowedCoin(uint160.Zero, escrow, clientEscrow, redeemDestination);
			client.AcceptPuzzle(puzzle.PuzzleValue);
			RoundTrip(ref client, parameters);
			Assert.True(client.GetInternalState().RedeemDestination == redeemDestination);
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
			var fulfill = server.FulfillOffer(clientOfferSig, new Key().ScriptPubKey, FeeRate);			
			var offerRedeem = client.CreateOfferRedeemTransaction(FeeRate);

			var offerTransaction = server.GetSignedOfferTransaction();
			var offerCoin = offerTransaction.Transaction.Outputs.AsCoins().First();
			var resigned = offerTransaction.ReSign(client.EscrowedCoin);

			TransactionBuilder txBuilder = new TransactionBuilder();
			txBuilder.AddCoins(client.EscrowedCoin);
			Assert.True(txBuilder.Verify(resigned));

			bool cached;
			resigned = fulfill.ReSign(offerCoin, out cached);
			Assert.False(cached);
			txBuilder = new TransactionBuilder();
			txBuilder.AddCoins(offerCoin);
			Assert.True(txBuilder.Verify(resigned));

			//Test again to see if cached signature works well
			resigned = fulfill.ReSign(offerCoin, out cached);
			Assert.True(cached);
			Assert.True(txBuilder.Verify(resigned));

			var offerRedeemTx = offerRedeem.ReSign(offerCoin);
			txBuilder = new TransactionBuilder();
			txBuilder.AddCoins(offerCoin);
			Assert.True(txBuilder.Verify(offerRedeemTx));


			client.CheckSolutions(fulfill.Transaction);
			RoundTrip(ref client, parameters);

			var clientEscapeSignature = client.SignEscape();
			var escapeTransaction = server.GetSignedEscapeTransaction(clientEscapeSignature, FeeRate, new Key().ScriptPubKey);

			txBuilder = new TransactionBuilder();
			txBuilder.AddCoins(client.EscrowedCoin);
			Assert.True(txBuilder.Verify(escapeTransaction));

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

		private void RoundTrip<T>(ref T[] commitments) where T : IBitcoinSerializable, new()
		{
			RoundtripJson(ref commitments);
			var a = new ArrayWrapper<T>(commitments);
			Roundtrip(ref a);
			commitments = a.Elements;
		}

		private void RoundTrip<T>(ref T commitments) where T : IBitcoinSerializable, new()
		{
			RoundtripJson(ref commitments);
			Roundtrip(ref commitments);
		}

		private void Roundtrip<T>(ref T commitments) where T : IBitcoinSerializable, new()
		{
			commitments = commitments.Clone();
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

		private LockTime EscrowDate = new LockTime(new DateTimeOffset(1988, 07, 18, 0, 0, 0, TimeSpan.Zero));
		private Money Amount = Money.Coins(1.0m);

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
