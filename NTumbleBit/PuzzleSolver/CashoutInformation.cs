using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
    public class CashoutInformation
    {
		public Script Cashout
		{
			get; set;
		}
		public Money Fee
		{
			get; set;
		}
	}
}
