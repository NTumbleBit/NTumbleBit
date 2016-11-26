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

		public PromiseParameters(RsaKey serverKey):this()
		{
			if(serverKey == null)
				throw new ArgumentNullException("serverKey");
			ServerKey = serverKey;
		}

		public RsaKey ServerKey
		{
			get; set;
		}
	}
}
