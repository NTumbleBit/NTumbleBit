using NBitcoin.DataEncoders;
using NTumbleBit.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class PuzzleSolution
	{
		public PuzzleSolution(byte[] solution)
		{
			if(solution == null)
				throw new ArgumentNullException(nameof(solution));
			_Value = new BigInteger(1, solution);
		}

		internal PuzzleSolution(BigInteger value)
		{
			if(value == null)
				throw new ArgumentNullException(nameof(value));
			_Value = value;
		}

		internal readonly BigInteger _Value;

		public byte[] ToBytes()
		{
			return _Value.ToByteArrayUnsigned();
		}

		public override bool Equals(object obj)
		{
			PuzzleSolution item = obj as PuzzleSolution;
			if(item == null)
				return false;
			return _Value.Equals(item._Value);
		}
		public static bool operator ==(PuzzleSolution a, PuzzleSolution b)
		{
			if(ReferenceEquals(a, b))
				return true;
			if(((object)a == null) || ((object)b == null))
				return false;
			return a._Value.Equals(b._Value);
		}

		public static bool operator !=(PuzzleSolution a, PuzzleSolution b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return _Value.GetHashCode();
		}

		public PuzzleSolution Unblind(RsaPubKey rsaPubKey, BlindFactor blind)
		{
			if(rsaPubKey == null)
				throw new ArgumentNullException(nameof(rsaPubKey));
			if(blind == null)
				throw new ArgumentNullException(nameof(blind));
			return new PuzzleSolution(rsaPubKey.Unblind(_Value, blind));
		}

		public override string ToString()
		{
			return Encoders.Hex.EncodeData(ToBytes());
		}
	}
}
