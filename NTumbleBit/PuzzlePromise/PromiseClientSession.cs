using NBitcoin;
using NBitcoin.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
	public class PromiseClientSession
	{
		class HashBase
		{
			public uint256 Hash
			{
				get; set;
			}
			public int Index
			{
				get;set;
			}
		}
		class RealHash : HashBase
		{
			public LockTime LockTime
			{
				get; set;
			}
		}

		class FakeHash : HashBase
		{
			public uint256 Salt
			{
				get; set;
			}
		}

		public PromiseClientSession(PromiseParameters parameters = null)
		{
			_Parameters = parameters ?? new PromiseParameters();
		}


		private readonly PromiseParameters _Parameters;
		public PromiseParameters Parameters
		{
			get
			{
				return _Parameters;
			}
		}

		HashBase[] _Hashes;

		public uint256[] CreateTransactionHashes(ICoin escrowCoin, Transaction cashoutTransaction)
		{
			if(escrowCoin == null)
				throw new ArgumentNullException("escrowCoin");
			if(cashoutTransaction == null)
				throw new ArgumentNullException("cashoutTransaction");
			cashoutTransaction = cashoutTransaction.Clone();
			List<HashBase> hashes = new List<HashBase>();
			LockTime lockTime = new LockTime(0);
			for(int i = 0; i < Parameters.RealTransactionCount; i++)
			{
				RealHash h = new RealHash();
				cashoutTransaction.LockTime = lockTime;
				h.Hash = cashoutTransaction.GetSignatureHash(escrowCoin);
				h.LockTime = lockTime;
				lockTime++;
				hashes.Add(h);
			}

			for(int i = 0; i < Parameters.FakeTransactionCount; i++)
			{
				FakeHash h = new FakeHash();
				h.Salt = new uint256(RandomUtils.GetBytes(32));
				h.Hash = Hashes.Hash256(Utils.Combine(h.Salt.ToBytes(), Parameters.FakeFormat.ToBytes()));
				hashes.Add(h);
			}

			_Hashes = hashes.ToArray();
			NBitcoin.Utils.Shuffle(_Hashes, RandomUtils.GetInt32());
			for(int i = 0; i < _Hashes.Length; i++)
			{
				_Hashes[i].Index = i;
			}
			//var fakeIndexesHash = Hashes.Hash256(PromiseUtils.IndexesToBytes(_Hashes.OfType<FakeHash>().Select(h => h.Index)));
			return _Hashes.Select(h => h.Hash).ToArray();
		}
	}
}
