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
	public static class Utils
	{
		public static IEnumerable<T> TopologicalSort<T>(this IEnumerable<T> nodes,
												Func<T, IEnumerable<T>> dependsOn)
		{
			List<T> result = new List<T>();
			var elems = nodes.ToDictionary(node => node,
										   node => new HashSet<T>(dependsOn(node)));
			while(elems.Count > 0)
			{
				var elem = elems.FirstOrDefault(x => x.Value.Count == 0);
				if(elem.Key == null)
				{
					//cycle detected can't order
					return nodes;
				}
				elems.Remove(elem.Key);
				foreach(var selem in elems)
				{
					selem.Value.Remove(elem.Key);
				}
				result.Add(elem.Key);
			}
			return result;
		}
		internal static byte[] ChachaEncrypt(byte[] data, ref byte[] key)
		{
			byte[] iv = null;
			return ChachaEncrypt(data, ref key, ref iv);
		}

		public static byte[] Combine(params byte[][] arrays)
		{
			var len = arrays.Select(a => a.Length).Sum();
			int offset = 0;
			var combined = new byte[len];
			foreach(var array in arrays)
			{
				Array.Copy(array, 0, combined, offset, array.Length);
				offset += array.Length;
			}
			return combined;
		}
		internal static byte[] ChachaEncrypt(byte[] data, ref byte[] key, ref byte[] iv)
		{
			ChaChaEngine engine = new ChaChaEngine();
			key = key ?? RandomUtils.GetBytes(ChachaKeySize);
			iv = iv ?? RandomUtils.GetBytes(ChachaKeySize / 2);
			engine.Init(true, new ParametersWithIV(new KeyParameter(key), iv));
			byte[] result = new byte[iv.Length + data.Length];
			Array.Copy(iv, result, iv.Length);
			engine.ProcessBytes(data, 0, data.Length, result, iv.Length);
			return result;
		}

		internal const int ChachaKeySize = 128 / 8;
		internal static byte[] ChachaDecrypt(byte[] encrypted, byte[] key)
		{
			ChaChaEngine engine = new ChaChaEngine();
			var iv = new byte[ChachaKeySize / 2];
			Array.Copy(encrypted, iv, iv.Length);
			engine.Init(false, new ParametersWithIV(new KeyParameter(key), iv));
			byte[] result = new byte[encrypted.Length - iv.Length];
			engine.ProcessBytes(encrypted, iv.Length, encrypted.Length - iv.Length, result, 0);
			return result;
		}

		internal static void Pad(ref byte[] bytes, int keySize)
		{
			int paddSize = keySize - bytes.Length;
			if(bytes.Length == keySize)
				return;
			if(paddSize < 0)
				throw new InvalidOperationException("Bug in NTumbleBit, copy the stacktrace and send us");
			var padded = new byte[paddSize + bytes.Length];
			Array.Copy(bytes, 0, padded, paddSize, bytes.Length);
			bytes = padded;
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
