using NBitcoin;
using NTumbleBit.BouncyCastle.Crypto.Digests;
using NTumbleBit.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
	internal static class PromiseUtils
	{

		public static byte[] SHA512(byte[] data, int offset, int count)
		{
			Sha512Digest sha512 = new Sha512Digest();
			sha512.BlockUpdate(data, offset, count);
			byte[] rv = new byte[64];
			sha512.DoFinal(rv, 0);
			return rv;
		}

		public static uint256 HashIndexes(ref uint256 salt, IEnumerable<int> indexes)
		{
			salt = salt ?? new uint256(RandomUtils.GetBytes(32));
			var bytes = Utils.Combine(indexes.Select(i => NBitcoin.Utils.ToBytes((uint)i, true)).ToArray());
			return new uint256(HMACSHA256(salt.ToBytes(), bytes));
		}

		private static byte[] HMACSHA256(byte[] key, byte[] data)
		{
			var mac = new BouncyCastle.Crypto.Macs.HMac(new Sha256Digest());
			mac.Init(new KeyParameter(key));
			mac.BlockUpdate(data, 0, data.Length);
			byte[] result = new byte[mac.GetMacSize()];
			mac.DoFinal(result, 0);
			return result;
		}
	}
}
