using NBitcoin;
using NBitcoin.Crypto;
using NTumbleBit.BouncyCastle.Asn1;
using NTumbleBit.BouncyCastle.Asn1.Pkcs;
using NTumbleBit.BouncyCastle.Asn1.X509;
using NTumbleBit.BouncyCastle.Crypto.Digests;
using NTumbleBit.BouncyCastle.Crypto.Engines;
using NTumbleBit.BouncyCastle.Crypto.Generators;
using NTumbleBit.BouncyCastle.Crypto.Parameters;
using NTumbleBit.BouncyCastle.Math;
using NTumbleBit.PuzzlePromise;
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

		public bool Verify(byte[] signature, byte[] data, uint160 nonce)
		{
			byte[] output = new byte[256];
			var msg = Utils.Combine(nonce.ToBytes(), data);
			Sha512Digest sha512 = new Sha512Digest();
			var generator = new Mgf1BytesGenerator(sha512);
			generator.Init(new MgfParameters(msg));
			generator.GenerateBytes(output, 0, output.Length);
			var input = new BigInteger(1, output);
			if(input.CompareTo(_Key.Modulus) >= 0)
				return false;
			if(signature.Length > 256)
				return false;
			var signatureInt = new BigInteger(1, signature);
			if(signatureInt.CompareTo(_Key.Modulus) >= 0)
				return false;
			var engine = new RsaCoreEngine();
			engine.Init(false, _Key);
			return input.Equals(engine.ProcessBlock(signatureInt));
		}

		public uint256 GetHash()
		{
			return Hashes.Hash256(ToBytes());
		}

		public Puzzle GeneratePuzzle(ref PuzzleSolution solution)
		{
			solution = solution ?? new PuzzleSolution(Utils.GenerateEncryptableInteger(_Key));
			return new Puzzle(this, new PuzzleValue(Encrypt(solution._Value)));
		}

		internal BigInteger Encrypt(BigInteger data)
		{
			if(data == null)
				throw new ArgumentNullException("data");
			if(data.CompareTo(_Key.Modulus) >= 0)
				throw new ArgumentException("input too large for RSA cipher.");
			RsaCoreEngine engine = new RsaCoreEngine();
			engine.Init(true, _Key);
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
			if(ReferenceEquals(a, b))
				return true;
			if(((object)a == null) || ((object)b == null))
				return false;
			return a._Key.Equals(b._Key);
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
