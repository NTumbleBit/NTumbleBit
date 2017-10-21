using NBitcoin;
using NTumbleBit.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
    public class Quotient : IBitcoinSerializable
    {
		internal BigInteger _Value;
		public Quotient()
		{

		}

		public Quotient(byte[] quotient)
		{
			if(quotient == null)
				throw new ArgumentNullException(nameof(quotient));
			_Value = new BigInteger(1, quotient);
		}

		internal Quotient(BigInteger quotient)
		{
            _Value = quotient ?? throw new ArgumentNullException(nameof(quotient));
		}

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWriteC(ref _Value);
		}
	}
}
