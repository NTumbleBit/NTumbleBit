using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
			Assert.Throws<FormatException>(() => new RsaKey(new byte[1]));
		}

		[Fact]
		public void CanSolvePuzzle()
		{
			RsaKey key = TestKeys.Default;
			byte[] solution = null;
			var puzzle = key.PubKey.GeneratePuzzle(ref solution);
			byte[] solution2 = puzzle.Solve(key);
			Assert.True(solution.SequenceEqual(solution2));
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
				data = Utils.GenerateEncryptableData(key._Key);
				signature = key.Sign(data);
				Assert.True(key.PubKey.Verify(data, signature));
			}


		}

		[Fact]
		public void CanRSASign()
		{
			RsaKey key = TestKeys.Default;
			var data = RandomUtils.GetBytes(RsaKey.KeySize);
			var sig = key.Sign(data);
			Assert.True(key.PubKey.Verify(data, sig));
		}


		[Fact]
		public void CanBlind()
		{

			RsaKey key = TestKeys.Default;

			byte[] msg = Utils.GenerateEncryptableData(key._Key);

			Blind blind = null;
			var blindedMsg = key.Blind(msg, ref blind);
			var blindedMsg2 = key.Blind(msg, ref blind);
			Assert.True(blindedMsg.SequenceEqual(blindedMsg2));

			var sig = key.Sign(blindedMsg);
			var sig2 = key.Sign(blindedMsg2);

			var unblindedSig = key.Unblind(sig, blind);
			var unblindedSig2 = key.Unblind(sig2, blind);
			Assert.True(key.PubKey.Verify(msg, unblindedSig));

			var unblindMsg = key.PubKey.RevertBlind(blindedMsg, blind);
			Assert.True(msg.SequenceEqual(unblindMsg));
		}
	}
}
