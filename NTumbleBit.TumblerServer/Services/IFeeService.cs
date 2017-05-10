using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#if !CLIENT
namespace NTumbleBit.TumblerServer.Services
#else
namespace NTumbleBit.Client.Tumbler.Services
#endif
{	
	public class FeeRateUnavailableException : Exception
	{
		public FeeRateUnavailableException()
		{
		}
		public FeeRateUnavailableException(string message) : base(message) { }
		public FeeRateUnavailableException(string message, Exception inner) : base(message, inner) { }
	}
	public interface IFeeService
    {
		FeeRate GetFeeRate();
    }
}
