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
	public class ClassicTumblerParameters
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

		public Network Network
		{
			get; set;
		}

		public OverlappedCycleGenerator CycleGenerator
		{
			get;
			set;
		}

		public RsaPubKey ServerKey
		{
			get; set;
		}
		public RsaPubKey VoucherKey
		{
			get; set;
		}

		public Money Denomination
		{
			get; set;
		}

		public Money Fee
		{
			get; set;
		}

		public int FakePuzzleCount
		{
			get; set;
		}
		public int RealPuzzleCount
		{
			get; set;
		}
		public int FakeTransactionCount
		{
			get; set;
		}
		public int RealTransactionCount
		{
			get; set;
		}
		public uint256 FakeFormat
		{
			get; set;
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

		public uint160 GetHash()
		{
			// Ideally ClassicTumblerParameters should be serialized as a byte array
			// Because there is no such thing as canonical JSON
			// For V1, we assume everybody run NTumbleBit, so it should not be a problem
			return Hashes.Hash160(Encoding.UTF8.GetBytes(Serializer.ToString(this, Network)));
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
