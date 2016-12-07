using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#if !CLIENT
namespace NTumbleBit.TumblerServer.Models
#else
namespace NTumbleBit.Client.Tumbler.Models
#endif
{
	public class AskVoucherResponse
    {
		public int Cycle
		{
			get; set;
		}
		public PuzzleValue UnsignedVoucher
		{
			get; set;
		}
	}
}
