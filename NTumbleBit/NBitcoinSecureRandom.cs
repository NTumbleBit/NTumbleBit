using NBitcoin;
using NTumbleBit.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	internal class NBitcoinSecureRandom : SecureRandom
	{

		private static readonly NBitcoinSecureRandom _Instance = new NBitcoinSecureRandom();
		public static NBitcoinSecureRandom Instance
		{
			get
			{
				return _Instance;
			}
		}
		private NBitcoinSecureRandom()
		{

		}

		public override void NextBytes(byte[] buffer)
		{
			RandomUtils.GetBytes(buffer);
		}

	}
}
