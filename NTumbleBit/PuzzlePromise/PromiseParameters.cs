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

		public PromiseParameters()
		{
			FakeTransactionCount = 42;
			RealTransactionCount = 42;
		}
	}
}
