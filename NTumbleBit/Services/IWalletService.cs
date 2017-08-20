using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.Services
{
	public interface IWalletService
    {
		IDestination GenerateAddress();
		Task<Transaction> FundTransactionAsync(TxOut txOut, FeeRate feeRate);
	}
}
