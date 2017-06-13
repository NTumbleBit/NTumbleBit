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
    public interface ITrustedBroadcastService
    {
		void Broadcast(int cycleStart, TransactionType transactionType, uint correlation, TrustedBroadcastRequest broadcast);
		TrustedBroadcastRequest GetKnownTransaction(uint256 txId);
		Transaction[] TryBroadcast(ref uint256[] knownBroadcasted);
		Transaction[] TryBroadcast();

	}
}
