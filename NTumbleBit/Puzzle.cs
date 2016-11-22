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

		public Puzzle Blind(IRsaKey rsaKey, ref byte[] blindFactor)
		{
			if(rsaKey == null)
				throw new ArgumentNullException("rsaKey");
			return Blind(((IRsaKeyPrivate)rsaKey).Key, true, ref blindFactor);
		}

		
		
		public Puzzle Unblind(IRsaKey rsaKey, byte[] blindFactor)
		{
			if(rsaKey == null)
				throw new ArgumentNullException("rsaKey");
			if(blindFactor == null)
				throw new ArgumentNullException("blindFactor");
			return Blind(((IRsaKeyPrivate)rsaKey).Key, false, ref blindFactor);
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

		private Puzzle Blind(RsaKeyParameters key, bool encryption, ref byte[] blindFactor)
		{
			blindFactor = blindFactor ?? Utils.GenerateEncryptableData(key);
			var factor = Utils.FromBytes(blindFactor);
			var engine = new RsaBlindingEngine();
			engine.Init(encryption, new RsaBlindingParameters(key, factor));
			return new Puzzle(engine.ProcessBlock(z, 0, z.Length));
		}
		
	}
}
