using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
	public class ClassicTumblerParameters : IBitcoinSerializable
	{
		public ClassicTumblerParameters()
		{
			var solver = new SolverParameters();
			FakePuzzleCount = solver.FakePuzzleCount;
			RealPuzzleCount = solver.RealPuzzleCount;

			var promise = new PromiseParameters();
			FakeTransactionCount = promise.FakeTransactionCount;
			RealTransactionCount = promise.RealTransactionCount;
			FakeFormat = promise.FakeFormat;

			Denomination = Money.Coins(1.0m);
			Fee = Money.Coins(0.01m);
			CycleGenerator = new OverlappedCycleGenerator();
		}

		Network _Network;
		public Network Network
		{
			get
			{
				return _Network;
			}
			set
			{
				_Network = value;
			}
		}


		OverlappedCycleGenerator _CycleGenerator;
		public OverlappedCycleGenerator CycleGenerator
		{
			get
			{
				return _CycleGenerator;
			}
			set
			{
				_CycleGenerator = value;
			}
		}


		RsaPubKey _ServerKey;
		public RsaPubKey ServerKey
		{
			get
			{
				return _ServerKey;
			}
			set
			{
				_ServerKey = value;
			}
		}


		RsaPubKey _VoucherKey;
		public RsaPubKey VoucherKey
		{
			get
			{
				return _VoucherKey;
			}
			set
			{
				_VoucherKey = value;
			}
		}


		Money _Denomination;
		public Money Denomination
		{
			get
			{
				return _Denomination;
			}
			set
			{
				_Denomination = value;
			}
		}


		Money _Fee;
		public Money Fee
		{
			get
			{
				return _Fee;
			}
			set
			{
				_Fee = value;
			}
		}


		int _FakePuzzleCount;
		public int FakePuzzleCount
		{
			get
			{
				return _FakePuzzleCount;
			}
			set
			{
				_FakePuzzleCount = value;
			}
		}


		int _RealPuzzleCount;
		public int RealPuzzleCount
		{
			get
			{
				return _RealPuzzleCount;
			}
			set
			{
				_RealPuzzleCount = value;
			}
		}


		int _FakeTransactionCount;
		public int FakeTransactionCount
		{
			get
			{
				return _FakeTransactionCount;
			}
			set
			{
				_FakeTransactionCount = value;
			}
		}


		int _RealTransactionCount;
		public int RealTransactionCount
		{
			get
			{
				return _RealTransactionCount;
			}
			set
			{
				_RealTransactionCount = value;
			}
		}


		uint256 _FakeFormat;
		public uint256 FakeFormat
		{
			get
			{
				return _FakeFormat;
			}
			set
			{
				_FakeFormat = value;
			}
		}

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWriteC(ref _Network);
			stream.ReadWrite(ref _CycleGenerator);
			stream.ReadWriteC(ref _ServerKey);
			stream.ReadWriteC(ref _VoucherKey);
			stream.ReadWriteC(ref _Denomination);
			stream.ReadWriteC(ref _Fee);
			stream.ReadWrite(ref _FakePuzzleCount);
			stream.ReadWrite(ref _RealPuzzleCount);
			stream.ReadWrite(ref _FakeTransactionCount);
			stream.ReadWrite(ref _RealTransactionCount);
		}

		public bool Check(PromiseParameters promiseParams)
		{
			return promiseParams.FakeTransactionCount == FakeTransactionCount &&
				promiseParams.RealTransactionCount == RealTransactionCount;
		}

		public bool Check(SolverParameters solverParams)
		{
			return solverParams.FakePuzzleCount == FakePuzzleCount &&
				solverParams.RealPuzzleCount == RealPuzzleCount;
		}

		public SolverParameters CreateSolverParamaters()
		{
			return new SolverParameters
			{
				FakePuzzleCount = FakePuzzleCount,
				RealPuzzleCount = RealPuzzleCount,
				ServerKey = ServerKey
			};
		}

		public PromiseParameters CreatePromiseParamaters()
		{
			return new PromiseParameters
			{
				FakeFormat = FakeFormat,
				FakeTransactionCount = FakeTransactionCount,
				RealTransactionCount = RealTransactionCount,
				ServerKey = ServerKey
			};
		}


		public override bool Equals(object obj)
		{
			ClassicTumblerParameters item = obj as ClassicTumblerParameters;
			if(item == null)
				return false;
			return GetHash().Equals(item.GetHash());
		}

		public bool IsStandard()
		{
			//TODO check RSA proof for the pubkeys
			return
				this.FakePuzzleCount == 285 &&
				this.RealPuzzleCount == 15 &&
				this.RealTransactionCount == 42 &&
				this.FakeTransactionCount == 42 &&
				this.Fee < this.Denomination &&
				this.FakeFormat == new uint256(Enumerable.Range(0, 32).Select(o => o == 0 ? (byte)0 : (byte)1).ToArray());

		}


		public uint160 GetHash()
		{
			return Hashes.Hash160(this.ToBytes());
		}

		public static bool operator ==(ClassicTumblerParameters a, ClassicTumblerParameters b)
		{
			if(System.Object.ReferenceEquals(a, b))
				return true;
			if(((object)a == null) || ((object)b == null))
				return false;
			return a.GetHash() == b.GetHash();
		}

		public static bool operator !=(ClassicTumblerParameters a, ClassicTumblerParameters b)
		{
			return !(a == b);
		}

		public static uint160 ExtractHashFromUrl(Uri serverUrl)
		{
			var prefix = "/api/v1/tumblers/";
			var path = new UriBuilder(serverUrl).Path;
			if(!path.StartsWith(prefix, StringComparison.Ordinal) || path.Length != prefix.Length + 20 * 2)
			{
				throw new FormatException("invalid tumbler uri");
			}
			return new uint160(path.Substring(prefix.Length, 20 * 2));
		}

		public override int GetHashCode()
		{
			return GetHash().GetHashCode();
		}
	}
}
