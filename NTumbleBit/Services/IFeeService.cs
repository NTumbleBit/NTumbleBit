using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.Services
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
		Task<FeeRate> GetFeeRateAsync();
    }
}
