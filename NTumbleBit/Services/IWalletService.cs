using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.Services
{
	public interface IWalletService
    {
		Task<IDestination> GenerateAddressAsync();
		Task<Transaction> FundTransactionAsync(TxOut txOut, FeeRate feeRate);
		Task<Transaction> ReceiveAsync(ScriptCoin escrowedCoin, TransactionSignature clientSignature, Key escrowKey, FeeRate feeRate);
	}
}
