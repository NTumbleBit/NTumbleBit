using NBitcoin;
using NBitcoin.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
	public class SolutionKey
	{
		public SolutionKey(byte[] key)
		{
			if(key == null)
				throw new ArgumentNullException(nameof(key));
			if((key.Length != KeySize))
				throw new ArgumentException("Chacha requires 128 bit key");
			_Bytes = key.ToArray();
		}

		private byte[] _Bytes;
		public static readonly long KeySize = 16;

		public byte[] ToBytes(bool @unsafe)
		{
			return @unsafe ? _Bytes : _Bytes.ToArray();
		}

		public uint160 GetHash()
		{
			return new uint160(Hashes.RIPEMD160(_Bytes, _Bytes.Length));
		}
	}
}
