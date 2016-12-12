using NBitcoin;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
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
		public ClassicTumblerParameters(RsaPubKey rsaKey, RsaPubKey voucherKey) : this()
		{
			if(rsaKey == null)
				throw new ArgumentNullException("rsaKey");
			if(voucherKey == null)
				throw new ArgumentNullException("voucherKey");
			ServerKey = rsaKey;
			VoucherKey = voucherKey;
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
			return new SolverParameters()
			{
				FakePuzzleCount = FakePuzzleCount,
				RealPuzzleCount = RealPuzzleCount,
				ServerKey = ServerKey
			};
		}

		public PromiseParameters CreatePromiseParamaters()
		{
			return new PromiseParameters()
			{
				FakeFormat = FakeFormat,
				FakeTransactionCount = FakeTransactionCount,
				RealTransactionCount = RealTransactionCount,
				ServerKey = ServerKey
			};
		}
	}
}
