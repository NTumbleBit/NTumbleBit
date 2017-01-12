using NBitcoin;
using NTumbleBit.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
	public class XORKey
	{
		public XORKey(PuzzleSolution puzzleSolution) : this(puzzleSolution._Value)
		{

		}
		public XORKey(RsaPubKey pubKey) : this(Utils.GenerateEncryptableInteger(pubKey._Key))
		{
		}
		public XORKey(byte[] key)
		{
			if(key == null)
				throw new ArgumentNullException("key");
			if(key.Length != KeySize)
				throw new ArgumentException("Key has invalid length from expected " + KeySize);
			_Value = new BigInteger(1, key);
		}

		private XORKey(BigInteger value)
		{
			if(value == null)
				throw new ArgumentNullException("value");

			_Value = value;
		}

		private BigInteger _Value;

		public byte[] XOR(byte[] data)
		{
			byte[] keyBytes = ToBytes();
			var keyHash = PromiseUtils.SHA512(keyBytes, 0, keyBytes.Length);
			var encrypted = new byte[data.Length];
			for(int i = 0; i < encrypted.Length; i++)
			{

				encrypted[i] = (byte)(data[i] ^ keyHash[i % keyHash.Length]);
			}
			return encrypted;
		}


		private const int KeySize = 256;
		public byte[] ToBytes()
		{
			byte[] keyBytes = _Value.ToByteArrayUnsigned();
			Utils.Pad(ref keyBytes, KeySize);
			return keyBytes;
		}
	}
}
