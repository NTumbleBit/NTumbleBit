using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

namespace NTumbleBit.TumblerServer.Services
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
		void Track(Script scriptPubkey);
	}
}
