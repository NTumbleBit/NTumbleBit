using NBitcoin;
using NBitcoin.Crypto;
using NTumbleBit.BouncyCastle.Asn1;
using NTumbleBit.BouncyCastle.Asn1.Pkcs;
using NTumbleBit.BouncyCastle.Asn1.X509;
using NTumbleBit.BouncyCastle.Crypto.Engines;
using NTumbleBit.BouncyCastle.Crypto.Parameters;
using NTumbleBit.BouncyCastle.Math;
using Org.BouncyCastle.Asn1.Pkcs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class RsaPubKey
	{
		public RsaPubKey()
		{

		}

		public RsaPubKey(byte[] bytes)
		{
			if(bytes == null)
				throw new ArgumentNullException("bytes");
			try
			{
				DerSequence seq2 = RsaKey.GetRSASequence(bytes);
				var s = new RsaPublicKeyStructure(seq2);
				_Key = new RsaKeyParameters(false, s.Modulus, s.PublicExponent);
			}
			catch(Exception)
			{
				throw new FormatException("Invalid RSA Key");
			}
		}

		internal readonly RsaKeyParameters _Key;
		internal RsaPubKey(RsaKeyParameters key)
		{
			if(key == null)
				throw new ArgumentNullException("key");
			_Key = key;
		}

		public byte[] ToBytes()
		{
			RsaPublicKeyStructure keyStruct = new RsaPublicKeyStructure(
				_Key.Modulus,
				_Key.Exponent);
			var privInfo = new PrivateKeyInfo(RsaKey.algID, keyStruct.ToAsn1Object());
			return privInfo.ToAsn1Object().GetEncoded();
		}

		public bool Verify(byte[] data, byte[] signature)
		{
			if(data == null)
				throw new ArgumentNullException("data");
			if(data.Length != RsaKey.KeySize / 8)
				throw new ArgumentException("data should have a size of " + RsaKey.KeySize + " bits");

			var engine = new RsaCoreEngine();
			engine.Init(false, _Key);
			return new BigInteger(1, data).Equals(engine.ProcessBlock(engine.ConvertInput(signature, 0, signature.Length)));
		}

		public uint256 GetHash()
		{
			return Hashes.Hash256(ToBytes());
		}

		public Puzzle GeneratePuzzle(ref PuzzleSolution solution)
		{
			solution = solution ?? new PuzzleSolution(Utils.GenerateEncryptableInteger(_Key));
			return new Puzzle(this, new PuzzleValue(this.Encrypt(solution._Value)));
		}

		internal BigInteger Encrypt(BigInteger data)
		{
			if(data == null)
				throw new ArgumentNullException("data");
			if(data.CompareTo(_Key.Modulus) >= 0)
				throw new ArgumentException("input too large for RSA cipher.");
			RsaCoreEngine engine = new RsaCoreEngine();
			engine.Init(true, this._Key);
			return engine.ProcessBlock(data);
		}

		internal BigInteger Blind(BigInteger data, ref BlindFactor blindFactor)
		{
			if(data == null)
				throw new ArgumentNullException("data");
			EnsureInitializeBlindFactor(ref blindFactor);
			return Blind(blindFactor._Value.ModPow(_Key.Exponent, _Key.Modulus), data);
		}

		private void EnsureInitializeBlindFactor(ref BlindFactor blindFactor)
		{
			blindFactor = blindFactor ?? new BlindFactor(Utils.GenerateEncryptableInteger(_Key));
		}

		internal BigInteger RevertBlind(BigInteger data, BlindFactor blindFactor)
		{
			if(data == null)
				throw new ArgumentNullException("data");
			if(blindFactor == null)
				throw new ArgumentNullException("blindFactor");
			EnsureInitializeBlindFactor(ref blindFactor);
			var ai = blindFactor._Value.ModInverse(_Key.Modulus);
			return Blind(ai.ModPow(_Key.Exponent, _Key.Modulus), data);
		}

		internal BigInteger Unblind(BigInteger data, BlindFactor blindFactor)
		{
			if(data == null)
				throw new ArgumentNullException("data");
			if(blindFactor == null)
				throw new ArgumentNullException("blindFactor");
			EnsureInitializeBlindFactor(ref blindFactor);
			return Blind(blindFactor._Value.ModInverse(_Key.Modulus), data);
		}

		internal BigInteger Blind(BigInteger multiplier, BigInteger msg)
		{
			return msg.Multiply(multiplier).Mod(_Key.Modulus);
		}


		public override bool Equals(object obj)
		{
			RsaPubKey item = obj as RsaPubKey;
			if(item == null)
				return false;
			return _Key.Equals(item._Key);
		}
		public static bool operator ==(RsaPubKey a, RsaPubKey b)
		{
			if(System.Object.ReferenceEquals(a, b))
				return true;
			if(((object)a == null) || ((object)b == null))
				return false;
			return a._Key == b._Key;
		}

		public static bool operator !=(RsaPubKey a, RsaPubKey b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return _Key.GetHashCode();
		}
	}
}
