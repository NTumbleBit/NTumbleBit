using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.TumblerServer.Services
{
    public interface IFeeService
    {
		FeeRate GetFeeRate();
    }
}
