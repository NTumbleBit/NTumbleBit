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
	public interface IWalletService
    {
		Key GenerateNewKey();
		IDestination GenerateAddress();
		Transaction FundTransaction(TxOut txOut, FeeRate feeRate);
	}
}
