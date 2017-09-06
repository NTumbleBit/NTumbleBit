using NBitcoin;
using NTumbleBit.BouncyCastle.Crypto.Engines;
using NTumbleBit.BouncyCastle.Crypto.Parameters;
using NTumbleBit.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public static class Utils
	{
		internal static HttpMessageHandler SetAntiFingerprint(this HttpClientHandler handler)
		{
			handler.AllowAutoRedirect = false;
			handler.UseCookies = false;
			handler.AutomaticDecompression = DecompressionMethods.None;
			handler.CheckCertificateRevocationList = false;
			handler.ClientCertificateOptions = ClientCertificateOption.Manual;
			handler.ClientCertificates.Clear();
			handler.CookieContainer = null;
			handler.Credentials = null;
			handler.PreAuthenticate = false;
			return handler;
		}

		public static void Shuffle<T>(T[] arr, Random rand)
		{
			rand = rand ?? new Random();
			for(int i = 0; i < arr.Length; i++)
			{
				var fromIndex = rand.Next(arr.Length);
				var from = arr[fromIndex];

				var toIndex = rand.Next(arr.Length);
				var to = arr[toIndex];

				arr[toIndex] = from;
				arr[fromIndex] = to;
			}
		}
		public static void Shuffle<T>(List<T> arr, Random rand)
		{
			rand = rand ?? new Random();
			for(int i = 0; i < arr.Count; i++)
			{
				var fromIndex = rand.Next(arr.Count);
				var from = arr[fromIndex];

				var toIndex = rand.Next(arr.Count);
				var to = arr[toIndex];

				arr[toIndex] = from;
				arr[fromIndex] = to;
			}
		}
		public static void Shuffle<T>(T[] arr, int seed)
		{
			Random rand = new Random(seed);
			Shuffle(arr, rand);
		}

		public static void Shuffle<T>(T[] arr)
		{
			Shuffle(arr, null);
		}


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

		// http://stackoverflow.com/a/14933880/2061103
		public static void DeleteRecursivelyWithMagicDust(string destinationDir)
		{
			const int magicDust = 10;
			for (var gnomes = 1; gnomes <= magicDust; gnomes++)
			{
				try
				{
					Directory.Delete(destinationDir, true);
				}
				catch (DirectoryNotFoundException)
				{
					return;  // good!
				}
				catch (IOException)
				{
					if (gnomes == magicDust)
						throw;
					// System.IO.IOException: The directory is not empty
					System.Diagnostics.Debug.WriteLine("Gnomes prevent deletion of {0}! Applying magic dust, attempt #{1}.", destinationDir, gnomes);

					// see http://stackoverflow.com/questions/329355/cannot-delete-directory-with-directory-deletepath-true for more magic
					Thread.Sleep(100);
					continue;
				}
				catch (UnauthorizedAccessException)
				{
					if (gnomes == magicDust)
						throw;
					// Wait, maybe another software make us authorized a little later
					System.Diagnostics.Debug.WriteLine("Gnomes prevent deletion of {0}! Applying magic dust, attempt #{1}.", destinationDir, gnomes);

					// see http://stackoverflow.com/questions/329355/cannot-delete-directory-with-directory-deletepath-true for more magic
					Thread.Sleep(100);
					continue;
				}
				return;
			}
			// depending on your use case, consider throwing an exception here
		}
	}
}
