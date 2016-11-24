﻿using NBitcoin;
using NBitcoin.DataEncoders;
using NTumbleBit.BouncyCastle.Crypto.Parameters;
using NTumbleBit.BouncyCastle.Math;
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
	public class Class1
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
		public void CanRSASign()
		{
			RsaKey key = TestKeys.Default;
			var data = RandomUtils.GetBytes(RsaKey.KeySize / 8);
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
		public void TestPuzzleSolver()
		{
			RsaKey key = TestKeys.Default;
			PuzzleSolution expectedSolution = null;
			Puzzle puzzle = key.PubKey.GeneratePuzzle(ref expectedSolution);
			
			var client = new SolverClientSession(puzzle);
			var server = new SolverServerSession(key);

			PuzzleValue[] puzzles = client.GeneratePuzzles();
			ServerCommitment[] commitments = server.SolvePuzzles(puzzles);
			ClientRevelation revelation = client.Reveal(commitments);
			SolutionKey[] fakePuzzleKeys = server.GetSolutionKeys(revelation);
			BlindFactor[] blindFactors = client.GetBlindFactors(fakePuzzleKeys);
			SolutionKey[] realPuzzleKeys = server.GetSolutionKeys(blindFactors);
			var solution = client.GetSolution(realPuzzleKeys);

			Assert.True(solution == expectedSolution);
		}

		[Fact]
		public void CanSerialize()
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
			var client = new SolverClientSession(puzzle.PuzzleValue, parameters);
			var server = new SolverServerSession(key, parameters);

			PuzzleValue[] puzzles = client.GeneratePuzzles();

			var ms = new MemoryStream();
			var seria = new SolverSerializer(client.Parameters, ms);
			seria.WritePuzzles(puzzles);
			ms.Position = 0;
			puzzles = seria.ReadPuzzles();

			var commitments = server.SolvePuzzles(puzzles);
			ms = new MemoryStream();
			seria = new SolverSerializer(client.Parameters, ms);
			seria.WritePuzzleCommitments(commitments);
			ms.Position = 0;
			commitments = seria.ReadPuzzleCommitments();

			var revelation = client.Reveal(commitments);
			ms = new MemoryStream();
			seria = new SolverSerializer(client.Parameters, ms);
			seria.WritePuzzleRevelation(revelation);
			ms.Position = 0;
			revelation = seria.ReadPuzzleRevelation();

			SolutionKey[] fakePuzzleKeys = server.GetSolutionKeys(revelation);
			ms = new MemoryStream();
			seria = new SolverSerializer(client.Parameters, ms);
			seria.WritePuzzleSolutionKeys(fakePuzzleKeys, false);
			ms.Position = 0;
			fakePuzzleKeys = seria.ReadPuzzleSolutionKeys(false);


			BlindFactor[] blindFactors = client.GetBlindFactors(fakePuzzleKeys);
			ms = new MemoryStream();
			seria = new SolverSerializer(client.Parameters, ms);
			seria.WriteBlindFactors(blindFactors);
			ms.Position = 0;
			blindFactors = seria.ReadBlindFactors();

			SolutionKey[] realPuzzleKeys = server.GetSolutionKeys(blindFactors);
			ms = new MemoryStream();
			seria = new SolverSerializer(client.Parameters, ms);
			seria.WritePuzzleSolutionKeys(realPuzzleKeys, true);
			ms.Position = 0;
			realPuzzleKeys = seria.ReadPuzzleSolutionKeys(true);

			var solution = client.GetSolution(realPuzzleKeys);
			Assert.True(solution == expectedSolution);
		}

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
