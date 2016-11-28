using NBitcoin;
using NBitcoin.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
	public class PromiseParameters
	{
		public int FakeTransactionCount
		{
			get;
			set;
		}
		public int RealTransactionCount
		{
			get;
			set;
		}

		public uint256 FakeFormat
		{
			get; set;
		}

		public PromiseParameters()
		{
			FakeTransactionCount = 42;
			RealTransactionCount = 42;
			FakeFormat = Hashes.Hash256(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 13, 14, 15, 16 });
		}

		public PromiseParameters(RsaPubKey serverKey) : this()
		{
			if(serverKey == null)
				throw new ArgumentNullException("serverKey");
			ServerKey = serverKey;
		}

		public int GetTotalTransactionsCount()
		{
			return FakeTransactionCount + RealTransactionCount;
		}

		public RsaPubKey ServerKey
		{
			get; set;
		}

		public uint256 CreateFakeHash(uint256 salt)
		{
			return Hashes.Hash256(Utils.Combine(salt.ToBytes(), FakeFormat.ToBytes()));
		}
	}
}
