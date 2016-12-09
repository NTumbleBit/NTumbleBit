using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

#if !CLIENT
namespace NTumbleBit.TumblerServer.Services
#else
namespace NTumbleBit.Client.Tumbler.Services
#endif
{
	public interface IBroadcastService
    {
		void Broadcast(params NBitcoin.Transaction[] transactions);
	}
}
