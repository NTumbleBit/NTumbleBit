using NTumbleBit.BouncyCastle.Asn1;
using NTumbleBit.BouncyCastle.Asn1.Pkcs;
using NTumbleBit.BouncyCastle.Asn1.X509;
using NTumbleBit.BouncyCastle.Crypto.Parameters;
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


		readonly RsaKeyParameters _Key;
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
	}
}
