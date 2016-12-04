using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Policy;
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

			var data = Encoders.Hex.DecodeData("0015faef3ea3d54858c1e8d84c4438ddd29f20031dd5229297b0b0d3c2565077be09c4df12cdd7220fd0ff3d500c72cb03e4da6927bb56c41a6152993e9fbcfbf2dc7894a88ec75a6a289b05506d41d6822ecc3046e73c5a4452aff0867de0d4c5d828040cc390cca992591371deb4648052c49a8f7b697453dc70507ba4d438fbf4b3a811954ff68e8b5c04c42b70c35ef71cc577b57680b6b164cf4c54c96797f3da02f7e3e71d8857cd0d7c2ea525ffbc5e5077b5573a3da5e983224a99771e39c23e5c754d61a8ac37b73b7021e962924ecd044373dc52fe01a6170f3fbfa4d9d9dfe09a49c9da786a60f29753b6bc4bd0b4630711872f1ba7fcad698f61");

			var signature = key.Sign(data);
			Assert.True(key.PubKey.Verify(data, signature));


			for(int i = 0; i < 100; i++)
			{
				data = GenerateEncryptableData(key._Key);
				signature = key.Sign(data);
				Assert.True(key.PubKey.Verify(data, signature));
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

			//The block 147 will be the first able to satisfy the lockTime
			Assert.Equal(146, parameter.GetClientLockTime().Height);


			var total = parameter.GetPeriods().Total;
			Assert.Equal(100, total.Start);
			Assert.Equal(160, total.End);

			//The block 160 will be the first able to satisfy the lockTime
			Assert.Equal(159, parameter.GetTumblerLockTime().Height);

			var cycleGenerator = new OverlappedCycleGenerator();
			cycleGenerator.FirstCycle = parameter;
			cycleGenerator.RegistrationOverlap = 3;
			Assert.Equal(100, cycleGenerator.GetRegistratingCycle(100).Start);
			Assert.Equal(100, cycleGenerator.GetRegistratingCycle(106).Start);
			Assert.Equal(107, cycleGenerator.GetRegistratingCycle(107).Start);
		}

		[Fact]
		public void CanRSASign()
		{
			RsaKey key = TestKeys.Default;
			var data = GenerateEncryptableData(key._Key);
			var sig = key.Sign(data);
			Assert.True(key.PubKey.Verify(data, sig));
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
	
		[Fact]
		public void TestPuzzlePromise()
		{
			RsaKey key = TestKeys.Default;

			Key serverKey = new Key();
			Key clientKey = new Key();

			var parameters = new PromiseParameters(key.PubKey)
			{
				FakeTransactionCount = 5,
				RealTransactionCount = 5
			};

			var client = new PromiseClientSession(parameters);
			var server = new PromiseServerSession(key, parameters);

			var coin = CreateEscrowCoin(serverKey.PubKey, clientKey.PubKey);

			Transaction cashout = new Transaction();
			cashout.AddInput(new TxIn(coin.Outpoint, Script.Empty));
			cashout.AddOutput(new TxOut(Money.Coins(1.5m), clientKey.PubKey.Hash));

			SignaturesRequest request = client.CreateSignatureRequest(coin, cashout);
			RoundTrip(ref client);
			RoundTrip(ref request, client.Parameters);

			PuzzlePromise.ServerCommitment[] commitments = server.SignHashes(request, serverKey);
			RoundTrip(ref server);
			RoundTrip(ref commitments, client.Parameters);

			PuzzlePromise.ClientRevelation revelation = client.Reveal(commitments);
			RoundTrip(ref client);
			RoundTrip(ref revelation, client.Parameters);

			ServerCommitmentsProof proof = server.CheckRevelation(revelation);
			RoundTrip(ref server);
			RoundTrip(ref proof, client.Parameters);

			var puzzleToSolve = client.CheckCommitmentProof(proof);
			RoundTrip(ref client);
			Assert.NotNull(puzzleToSolve);

			var solution = key.SolvePuzzle(puzzleToSolve);
			var transactions = client.GetSignedTransactions(solution).ToArray();
			RoundTrip(ref client);
			Assert.True(transactions.Length == parameters.RealTransactionCount);

			foreach(var tx in transactions)
			{
				TransactionBuilder builder = new TransactionBuilder();
				builder.AddCoins(coin);
				builder.AddKeys(clientKey);
				builder.StandardTransactionPolicy = new StandardTransactionPolicy()
				{
					CheckFee = false
				};
				Assert.False(builder.Verify(tx));
				builder.SignTransactionInPlace(tx);
				Assert.True(builder.Verify(tx));
			}
		}

		private void RoundTrip(ref ServerCommitmentsProof proof, PromiseParameters parameters)
		{
			var ms = new MemoryStream();
			var seria = new PromiseSerializer(parameters, ms);
			seria.WriteCommitmentsProof(proof);
			ms.Position = 0;
			proof = seria.ReadCommitmentsProof();
		}

		private void RoundTrip(ref PuzzlePromise.ClientRevelation revelation, PromiseParameters parameters)
		{
			var ms = new MemoryStream();
			var seria = new PromiseSerializer(parameters, ms);
			seria.WriteRevelation(revelation);
			ms.Position = 0;
			revelation = seria.ReadRevelation();
		}

		private void RoundTrip(ref PuzzlePromise.ServerCommitment[] commitments, PromiseParameters parameters)
		{
			var ms = new MemoryStream();
			var seria = new PromiseSerializer(parameters, ms);
			seria.WriteCommitments(commitments);
			ms.Position = 0;
			commitments = seria.ReadCommitments();
		}

		private void RoundTrip(ref SignaturesRequest request, PromiseParameters parameters)
		{
			var ms = new MemoryStream();
			var seria = new PromiseSerializer(parameters, ms);
			seria.WriteSignaturesRequest(request);
			ms.Position = 0;
			request = seria.ReadSignaturesRequest();
		}

		private ScriptCoin CreateEscrowCoin(PubKey pubKey, PubKey pubKey2)
		{
			var redeem = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, pubKey, pubKey2);
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

			PuzzleValue[] puzzles = client.GeneratePuzzles(puzzle.PuzzleValue);
			RoundTrip(ref client);
			RoundTrip(ref puzzles, client.Parameters);			

			var commitments = server.SolvePuzzles(puzzles);
			RoundTrip(ref server);
			RoundTrip(ref commitments, client.Parameters);

			var revelation = client.Reveal(commitments);
			RoundTrip(ref client);
			RoundTrip(ref revelation, client.Parameters);			

			SolutionKey[] fakePuzzleKeys = server.CheckRevelation(revelation);
			RoundTrip(ref server);
			RoundTrip(ref fakePuzzleKeys, false, client.Parameters);


			BlindFactor[] blindFactors = client.GetBlindFactors(fakePuzzleKeys);
			RoundTrip(ref client);
			RoundTrip(ref blindFactors, client.Parameters);

			server.CheckBlindedFactors(blindFactors);
			RoundTrip(ref server);
			SolutionKey[] realPuzzleKeys = server.GetSolutionKeys();
			RoundTrip(ref realPuzzleKeys, true, client.Parameters);

			var serverClone = SolverServerSession.ReadFrom(server.ToBytes(true));
			var clientClone = SolverClientSession.ReadFrom(client.ToBytes());
			client.CheckSolutions(realPuzzleKeys);
			RoundTrip(ref client);
			var solution = client.GetSolution();
			RoundTrip(ref client);
			Assert.True(solution == expectedSolution);

			client = clientClone;
			server = serverClone;
			//Verify if the scripts are correctly created
			Key serverKey = new Key();
			Key clientKey = new Key();
			PaymentCashoutContext ctx = new PaymentCashoutContext();
			ctx.RedeemKey = serverKey.PubKey;
			ctx.RefundKey = clientKey.PubKey;
			ctx.Expiration = EscrowDate;
			var offer = client.CreateOfferScript(ctx);
			Coin coin = new Coin(new OutPoint(), new TxOut(Money.Zero, offer.Hash)).ToScriptCoin(offer);
			Transaction fulfillTx = new Transaction();
			fulfillTx.Inputs.Add(new TxIn(coin.Outpoint));
			var sig = serverKey.Sign(Script.SignatureHash(coin, fulfillTx), SigHash.All);
			fulfillTx.Inputs[0].ScriptSig = server.GetFulfillScript(ctx, sig);
			ScriptError error;
			Assert.True(Script.VerifyScript(coin.ScriptPubKey, fulfillTx, 0, Money.Zero, out error));
			////////////////////////////////////////////////

			client.CheckSolutions(fulfillTx);
			RoundTrip(ref client);
			solution = client.GetSolution();
			RoundTrip(ref client);
			Assert.True(solution == expectedSolution);
		}

		private void RoundTrip(ref PuzzleValue[] puzzles, SolverParameters parameters)
		{
			var ms = new MemoryStream();
			var seria = new SolverSerializer(parameters, ms);
			seria.WritePuzzles(puzzles);
			ms.Position = 0;
			puzzles = seria.ReadPuzzles();
		}

		private void RoundTrip(ref PromiseServerSession server)
		{
			server = PromiseServerSession.ReadFrom(server.ToBytes(true));
		}

		private void RoundTrip(ref PromiseClientSession client)
		{
			client = PromiseClientSession.ReadFrom(client.ToBytes());
		}

		private void RoundTrip(ref BlindFactor[] blindFactors, SolverParameters parameters)
		{
			var ms = new MemoryStream();
			var seria = new SolverSerializer(parameters, ms);
			seria.WriteBlindFactors(blindFactors);
			ms.Position = 0;
			blindFactors = seria.ReadBlindFactors();
		}

		private void RoundTrip(ref SolutionKey[] fakePuzzleKeys, bool real, SolverParameters parameters)
		{
			var ms = new MemoryStream();
			var seria = new SolverSerializer(parameters, ms);
			seria.WritePuzzleSolutionKeys(fakePuzzleKeys, real);
			ms.Position = 0;
			fakePuzzleKeys = seria.ReadPuzzleSolutionKeys(real);
		}

		private void RoundTrip(ref PuzzleSolver.ClientRevelation revelation, SolverParameters parameters)
		{
			var ms = new MemoryStream();
			var seria = new SolverSerializer(parameters, ms);
			seria.WritePuzzleRevelation(revelation);
			ms.Position = 0;
			revelation = seria.ReadPuzzleRevelation();
		}

		private void RoundTrip(ref PuzzleSolver.ServerCommitment[] commitments, SolverParameters parameters)
		{
			var ms = new MemoryStream();
			var seria = new SolverSerializer(parameters, ms);
			seria.WritePuzzleCommitments(commitments);
			ms.Position = 0;
			commitments = seria.ReadPuzzleCommitments();
		}

		private void RoundTrip(ref SolverServerSession server)
		{
			var ms = new MemoryStream();
			server.WriteTo(ms, true);
			ms.Position = 0;
			server = SolverServerSession.ReadFrom(ms, null);
		}

		private void RoundTrip(ref SolverClientSession client)
		{
			var ms = new MemoryStream();
			client.WriteTo(ms);
			ms.Position = 0;
			client = SolverClientSession.ReadFrom(ms);
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
