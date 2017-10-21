using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class Puzzle
	{
		public Puzzle(RsaPubKey rsaPubKey, PuzzleValue puzzleValue)
		{
            _RsaPubKey = rsaPubKey ?? throw new ArgumentNullException(nameof(rsaPubKey));
			_PuzzleValue = puzzleValue ?? throw new ArgumentNullException(nameof(puzzleValue));
		}

		public Puzzle Blind(ref BlindFactor blind)
		{
			return new Puzzle(_RsaPubKey, new PuzzleValue(_RsaPubKey.Blind(PuzzleValue._Value, ref blind)));
		}

		public Puzzle Unblind(BlindFactor blind)
		{
			if(blind == null)
				throw new ArgumentNullException(nameof(blind));
			return new Puzzle(_RsaPubKey, new PuzzleValue(RsaPubKey.RevertBlind(PuzzleValue._Value, blind)));
		}

		public PuzzleSolution Solve(RsaKey key)
		{
			if(key == null)
				throw new ArgumentNullException(nameof(key));
			return PuzzleValue.Solve(key);
		}

		public bool Verify(PuzzleSolution solution)
		{
			if(solution == null)
				throw new ArgumentNullException(nameof(solution));
			return _RsaPubKey.Encrypt(solution._Value).Equals(PuzzleValue._Value);
		}


		private readonly RsaPubKey _RsaPubKey;
		public RsaPubKey RsaPubKey
		{
			get
			{
				return _RsaPubKey;
			}
		}


		private readonly PuzzleValue _PuzzleValue;
		public PuzzleValue PuzzleValue
		{
			get
			{
				return _PuzzleValue;
			}
		}


		public override bool Equals(object obj)
		{
			Puzzle item = obj as Puzzle;
			if(item == null)
				return false;
			return PuzzleValue.Equals(item.PuzzleValue) && RsaPubKey.Equals(item.RsaPubKey);
		}
		public static bool operator ==(Puzzle a, Puzzle b)
		{
			if(ReferenceEquals(a, b))
				return true;
			if(((object)a == null) || ((object)b == null))
				return false;
			return a.PuzzleValue == b.PuzzleValue && a.RsaPubKey == b.RsaPubKey;
		}

		public static bool operator !=(Puzzle a, Puzzle b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return PuzzleValue.GetHashCode() ^ RsaPubKey.GetHashCode();
		}

		public override string ToString()
		{
			return PuzzleValue.ToString();
		}
	}
}
