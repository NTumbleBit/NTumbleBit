using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.Services
{
    public interface ITrustedBroadcastService
    {
		void Broadcast(int cycleStart, TransactionType transactionType, CorrelationId correlation, TrustedBroadcastRequest broadcast);
		TrustedBroadcastRequest GetKnownTransaction(uint256 txId);
		Transaction[] TryBroadcast(ref uint256[] knownBroadcasted);
		Transaction[] TryBroadcast();

	}
}
