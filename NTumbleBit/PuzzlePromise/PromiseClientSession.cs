using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
	public class PromiseClientSession
	{
		public PromiseClientSession(PromiseParameters parameters = null)
		{
			_Parameters = parameters ?? PromiseParameters.CreateDefault();
		}


		private readonly PromiseParameters _Parameters;
		public PromiseParameters Parameters
		{
			get
			{
				return _Parameters;
			}
		}



	}
}
