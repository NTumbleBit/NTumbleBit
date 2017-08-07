using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.BouncyCastle.Security
{
	internal class SecureRandom : Random
	{
		public SecureRandom()
		{
		}

		public override int Next()
		{
			return RandomUtils.GetInt32();
		}

		public override int Next(int maxValue)
		{
			return (int)(RandomUtils.GetUInt32() % maxValue);
		}

		public override int Next(int minValue, int maxValue)
		{
			throw new NotImplementedException();
		}

		public override void NextBytes(byte[] buffer)
		{
			RandomUtils.GetBytes(buffer);
		}
	}
}
