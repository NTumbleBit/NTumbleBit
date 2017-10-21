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
			FakeFormat = new uint256(Enumerable.Range(0, 32).Select(o => o == 0 ? (byte)0 : (byte)1).ToArray());
		}

		public PromiseParameters(RsaPubKey serverKey) : this()
		{
            ServerKey = serverKey ?? throw new ArgumentNullException(nameof(serverKey));
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
