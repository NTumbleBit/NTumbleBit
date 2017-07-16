using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
	public class SignaturesRequest : IBitcoinSerializable
	{
		uint256[] _Hashes;
		public uint256[] Hashes
		{
			get
			{
				return _Hashes;
			}
			set
			{
				_Hashes = value;
			}
		}


		uint256 _FakeIndexesHash;
		public uint256 FakeIndexesHash
		{
			get
			{
				return _FakeIndexesHash;
			}
			set
			{
				_FakeIndexesHash = value;
			}
		}

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWriteC(ref _Hashes);
			stream.ReadWrite(ref _FakeIndexesHash);
		}
	}
}
