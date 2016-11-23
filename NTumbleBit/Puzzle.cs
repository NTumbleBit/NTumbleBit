using NBitcoin.DataEncoders;
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
		private readonly BigInteger _Value;

		public Puzzle(byte[] z)
		{
			if(z == null)
				throw new ArgumentNullException("z");
			this.z = z.ToArray();
			_Value = new BigInteger(1, z);
		}



		public Puzzle Blind(RsaPubKey rsaKey, ref Blind blind)
		{
			if(rsaKey == null)
				throw new ArgumentNullException("rsaKey");
			return new Puzzle(rsaKey.Blind(z, ref blind));
		}

		
		
		public Puzzle Unblind(RsaPubKey rsaKey, Blind blind)
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

		public bool Verify(RsaPubKey key, byte[] solution)
		{
			if(key == null)
				throw new ArgumentNullException("key");
			if(solution == null)
				throw new ArgumentNullException("solution");
			return new BigInteger(1, key.Encrypt(solution)).Equals(_Value);
		}

		public Puzzle RevertBlind(RsaPubKey key, BlindFactor blindFactor)
		{
			return new Puzzle(key.RevertBlind(z, new NTumbleBit.Blind(key, blindFactor.ToBytes())));
		}

		public byte[] ToBytes(bool @unsafe = false)
		{
			return @unsafe ? z : z.ToArray();
		}

		public override bool Equals(object obj)
		{
			Puzzle item = obj as Puzzle;
			if(item == null)
				return false;
			return _Value.Equals(item._Value);
		}
		public static bool operator ==(Puzzle a, Puzzle b)
		{
			if(System.Object.ReferenceEquals(a, b))
				return true;
			if(((object)a == null) || ((object)b == null))
				return false;
			return a._Value.Equals(b._Value);
		}

		public static bool operator !=(Puzzle a, Puzzle b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return _Value.GetHashCode();
		}

		public override string ToString()
		{
			return Encoders.Hex.EncodeData(z);
		}
	}
}
