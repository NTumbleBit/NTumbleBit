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
		internal readonly BigInteger _Value;

		public Puzzle(byte[] z)
		{
			if(z == null)
				throw new ArgumentNullException("z");
			_Value = new BigInteger(1, z);
		}
		public Puzzle(BigInteger z)
		{
			if(z == null)
				throw new ArgumentNullException("z");
			_Value = z;
		}


		public Puzzle Blind(RsaPubKey rsaKey, ref BlindFactor blind)
		{
			if(rsaKey == null)
				throw new ArgumentNullException("rsaKey");
			return new Puzzle(rsaKey.Blind(_Value, ref blind));
		}

		
		
		public Puzzle Unblind(RsaPubKey rsaKey, BlindFactor blind)
		{
			if(rsaKey == null)
				throw new ArgumentNullException("rsaKey");
			if(blind == null)
				throw new ArgumentNullException("blind");
			return new Puzzle(rsaKey.RevertBlind(_Value, new NTumbleBit.Blind(rsaKey, blind.ToBytes())));
		}

		public PuzzleSolution Solve(RsaKey key)
		{
			if(key == null)
				throw new ArgumentNullException("key");
			return key.SolvePuzzle(this);
		}

		public bool Verify(RsaPubKey key, PuzzleSolution solution)
		{
			if(key == null)
				throw new ArgumentNullException("key");
			if(solution == null)
				throw new ArgumentNullException("solution");
			return key.Encrypt(solution._Value).Equals(_Value);
		}

		public byte[] ToBytes()
		{
			return _Value.ToByteArrayUnsigned();
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
			return Encoders.Hex.EncodeData(ToBytes());
		}
	}
}
