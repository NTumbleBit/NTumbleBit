using NBitcoin;
using NTumbleBit.BouncyCastle.Crypto.Engines;
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
		public static byte[] ChachaEncrypt(byte[] data, ref byte[] key)
		{
			byte[] iv = null;
			return ChachaEncrypt(data, ref key, ref iv);
		}
		public static byte[] ChachaEncrypt(byte[] data, ref byte[] key, ref byte[] iv)
		{
			ChaChaEngine engine = new ChaChaEngine();
			key = key ?? RandomUtils.GetBytes(128 / 8);
			iv = iv ?? RandomUtils.GetBytes(64 / 8);
			engine.Init(true, new ParametersWithIV(new KeyParameter(key), iv));
			byte[] result = new byte[iv.Length + data.Length];
			Array.Copy(iv, result, iv.Length);
			engine.ProcessBytes(data, 0, data.Length, result, iv.Length);
			return result;
		}
		public static byte[] ChachaDecrypt(byte[] encrypted, byte[] key)
		{
			ChaChaEngine engine = new ChaChaEngine();
			var iv = new byte[(64 / 8)];
			Array.Copy(encrypted, iv, iv.Length);
			engine.Init(false, new ParametersWithIV(new KeyParameter(key), iv));
			byte[] result = new byte[encrypted.Length - iv.Length];
			engine.ProcessBytes(encrypted, iv.Length, encrypted.Length - iv.Length, result, 0);
			return result;
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

		internal static BigInteger GenerateEncryptableInteger(RsaKeyParameters key)
		{
			while(true)
			{
				var bytes = RandomUtils.GetBytes(RsaKey.KeySize / 8);
				BigInteger input = new BigInteger(1, bytes);
				if(input.CompareTo(key.Modulus) >= 0)
					continue;
				return input;
			}
		}
	}
}
