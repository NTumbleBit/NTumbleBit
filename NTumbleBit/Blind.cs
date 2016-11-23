using NTumbleBit.BouncyCastle.Math;
using NTumbleBit.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class Blind
	{
		public Blind(IRsaKey key) : this(((IRsaKeyPrivate)key).Key)
		{

		}
		public Blind(IRsaKey key, byte[] blind) : this(((IRsaKeyPrivate)key).Key, new BigInteger(1, blind))
		{

		}

		Blind(RsaKeyParameters key):this(key, Utils.GenerateEncryptableInteger(key))
		{
		}

		Blind(RsaKeyParameters key, BigInteger r)
		{
			if(r.CompareTo(key.Modulus) >= 0)
			{
				throw new ArgumentException("The blinding is too high");
			}

			var mod = key.Modulus;
			var e = key.Exponent;

			_R = r;
			_AI = r.ModInverse(mod);
			_A = r.ModPow(e, mod);
			_RI = _AI.ModPow(e, mod);
		}

		internal readonly BigInteger _R;
		internal readonly BigInteger _AI;
		internal readonly BigInteger _A;
		internal readonly BigInteger _RI;

		public byte[] ToBytes()
		{
			return _R.ToByteArrayUnsigned();
		}
	}
}
