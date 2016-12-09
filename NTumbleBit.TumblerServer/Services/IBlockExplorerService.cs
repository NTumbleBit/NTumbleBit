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
	public class TransactionInformation
	{
		public int Confirmations
		{
			get; set;
		}
		public Transaction Transaction
		{
			get; set;
		}
	}
    public interface IBlockExplorerService
    {
		int GetCurrentHeight();
		TransactionInformation GetTransaction(uint256 txId);
		TransactionInformation[] GetTransactions(Script scriptPubKey);
		void Track(Script scriptPubkey);
	}
}
