using NTumbleBit.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class BlindFactor
	{
		public BlindFactor(byte[] v)
		{
			if(v == null)
				throw new ArgumentNullException("v");
			_V = new BigInteger(1, v);
		}

		internal BlindFactor(BigInteger v)
		{
			if(v == null)
				throw new ArgumentNullException("v");
			_V = v;
		}

		BigInteger _V;

		public byte[] ToBytes()
		{
			return _V.ToByteArrayUnsigned();
		}
	}
}
