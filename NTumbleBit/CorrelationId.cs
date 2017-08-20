using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace NTumbleBit
{
	public class CorrelationId
	{
		public static CorrelationId Parse(string str)
		{
			uint v;
			//To eventually remove in august 2017, prevent old stuff from crashing
			if(uint.TryParse(str, out v))
				return new CorrelationId(v);
			///////
			return new CorrelationId(uint256.Parse(str));
		}
		public CorrelationId(uint256 id)
		{
			if(id == null)
				throw new ArgumentNullException(nameof(id));
			_Id = id;
		}

		public CorrelationId(Script redeem) : this(new uint256(redeem.WitHash.ToBytes()))
		{

		}

		public CorrelationId(ScriptCoin coin) : this(coin.Redeem)
		{
		}

		readonly uint256 _Id;
		public static readonly CorrelationId Zero = new CorrelationId(uint256.Zero);

		public override bool Equals(object obj)
		{
			CorrelationId item = obj as CorrelationId;
			if(item == null)
				return false;
			return _Id.Equals(item._Id);
		}
		public static bool operator ==(CorrelationId a, CorrelationId b)
		{
			if(System.Object.ReferenceEquals(a, b))
				return true;
			if(((object)a == null) || ((object)b == null))
				return false;
			return a._Id == b._Id;
		}

		public static bool operator !=(CorrelationId a, CorrelationId b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return _Id.GetHashCode();
		}


		public override string ToString()
		{
			return ToString(true);
		}

		public string ToString(bool longForm)
		{
			if(longForm)
				return _Id.ToString();
			return _Id.GetLow64().ToString();
		}

		public byte[] ToBytes()
		{
			return _Id.ToBytes();
		}
	}
}
