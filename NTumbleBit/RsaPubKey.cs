using NBitcoin;
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


		public Puzzle GeneratePuzzle(ref PuzzleSolution solution)
		{
			solution = solution ?? new PuzzleSolution(Utils.GenerateEncryptableData(_Key));
			return new Puzzle(this.Encrypt(solution._Value));
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
			Blind blind = CalculateBlind(ref blindFactor);
			return Blind(blind._A, data);
		}

		private Blind CalculateBlind(ref BlindFactor blindFactor)
		{
			var blind = blindFactor == null ? new Blind(this) : new Blind(_Key, blindFactor._Value);
			blindFactor = blindFactor ?? blind.ToBlindFactor();
			return blind;
		}

		internal BigInteger RevertBlind(BigInteger data, Blind blind)
		{
			if(data == null)
				throw new ArgumentNullException("data");
			if(blind == null)
				throw new ArgumentNullException("blind");

			return Blind(blind._RI, data);
		}

		internal BigInteger Unblind(BigInteger data, BlindFactor blindFactor)
		{
			if(data == null)
				throw new ArgumentNullException("data");
			if(blindFactor == null)
				throw new ArgumentNullException("blindFactor");
			Blind blind = CalculateBlind(ref blindFactor);
			return Blind(blind._AI, data);
		}

		internal BigInteger Blind(BigInteger multiplier, BigInteger msg)
		{
			return msg.Multiply(multiplier).Mod(_Key.Modulus);
		}
	}
}
