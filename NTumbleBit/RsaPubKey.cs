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
	public class RsaPubKey : IRsaKeyPrivate, IRsaKey
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


		RsaKeyParameters IRsaKeyPrivate.Key
		{
			get
			{
				return _Key;
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


		public Puzzle GeneratePuzzle(ref byte[] solution)
		{
			solution = solution ?? Utils.GenerateEncryptableData(_Key);
			return new Puzzle(Encrypt(solution));
		}

		public byte[] Encrypt(byte[] data)
		{
			if(data == null)
				throw new ArgumentNullException("data");
			if(data.Length != RsaKey.KeySize / 8)
				throw new ArgumentException("The data to be encrypted should be equal to size " + RsaKey.KeySize + " bits");
			RsaCoreEngine engine = new RsaCoreEngine();
			engine.Init(true, _Key);
			var databn = engine.ConvertInput(data, 0, data.Length);
			var resultbn = engine.ProcessBlock(databn);
			return engine.ConvertOutput(resultbn);
		}

		public byte[] Blind(byte[] data, ref Blind blind)
		{
			if(data == null)
				throw new ArgumentNullException("data");
			blind = blind ?? new Blind(this);
			return Blind(blind._A, data, blind);
		}

		public byte[] RevertBlind(byte[] data, Blind blind)
		{
			if(data == null)
				throw new ArgumentNullException("data");
			if(blind == null)
				throw new ArgumentNullException("blind");

			return Blind(blind._RI, data, blind);
		}

		public byte[] Unblind(byte[] data, Blind blind)
		{
			if(data == null)
				throw new ArgumentNullException("data");
			if(blind == null)
				throw new ArgumentNullException("blind");
			return Blind(blind._AI, data, blind);
		}

		internal byte[] Blind(BigInteger multiplier, byte[] data, Blind blind)
		{
			blind = blind ?? new Blind(this);
			var msg = new BigInteger(1, data);
			return msg.Multiply(multiplier).Mod(_Key.Modulus).ToByteArrayUnsigned();
		}
	}
}
