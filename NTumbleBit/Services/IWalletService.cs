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
		Transaction FundTransaction(TxOut txOut, FeeRate feeRate);
	}
}
