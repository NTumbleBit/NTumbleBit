using NBitcoin;
using NTumbleBit.BouncyCastle.Crypto.Parameters;
using NTumbleBit.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	internal static class Utils
	{
		public static byte[] ToBytes(BigInteger num)
		{
			if(num == null)
				throw new ArgumentNullException("num");
			return num.ToByteArrayUnsigned();
		}
		public static BigInteger FromBytes(byte[] data)
		{
			if(data == null)
				throw new ArgumentNullException("data");
			return new BigInteger(1, data);
		}

		public static byte[] GenerateEncryptableData(RsaKeyParameters key)
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
	}
}
