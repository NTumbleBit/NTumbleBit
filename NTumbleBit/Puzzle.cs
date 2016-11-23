using NTumbleBit.BouncyCastle.Crypto.Engines;
using NTumbleBit.BouncyCastle.Crypto.Parameters;
using NTumbleBit.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class Puzzle
	{
		private readonly byte[] z;

		public Puzzle(byte[] z)
		{
			if(z == null)
				throw new ArgumentNullException("z");
			this.z = z.ToArray();
		}

		public Puzzle Blind(IRsaKey rsaKey, ref Blind blind)
		{
			if(rsaKey == null)
				throw new ArgumentNullException("rsaKey");
			return new Puzzle(rsaKey.Blind(z, ref blind));
		}

		
		
		public Puzzle Unblind(IRsaKey rsaKey, Blind blind)
		{
			if(rsaKey == null)
				throw new ArgumentNullException("rsaKey");
			if(blind == null)
				throw new ArgumentNullException("blind");
			return new Puzzle(rsaKey.Unblind(z, blind));
		}

		public byte[] Solve(RsaKey key)
		{
			if(key == null)
				throw new ArgumentNullException("key");
			return key.SolvePuzzle(this);
		}

		public byte[] ToBytes(bool @unsafe = false)
		{
			return @unsafe ? z : z.ToArray();
		}
		
	}
}
