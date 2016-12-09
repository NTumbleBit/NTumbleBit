using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

namespace NTumbleBit.TumblerServer.Services
{
    public interface IBroadcastService
    {
		void Broadcast(params NBitcoin.Transaction[] transactions);
	}
}
