using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin.Crypto;

namespace NTumbleBit.PuzzlePromise
{
    public class CashoutTransaction
	{
		public CashoutTransaction(ICoin escrowedCoin, Transaction cashout)
		{
			if(escrowedCoin == null)
				throw new ArgumentNullException("escrowedCoin");
			if(cashout == null)
				throw new ArgumentNullException("cashout");
			_EscrowedCoin = escrowedCoin;
			_CashoutTransaction = cashout.Clone();
		}


		public PubKey[] GetExpectedSigners()
		{
			var multiSig = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(_EscrowedCoin.GetScriptCode());
			if(multiSig == null || multiSig.SignatureCount != 2 || multiSig.InvalidPubKeys.Length != 0 || multiSig.PubKeys.Length != 2)
				throw new ArgumentException("Invalid escrow 2-2 multisig");
			return multiSig.PubKeys;
		}

		private readonly Transaction _CashoutTransaction;
		public Transaction Transaction
		{
			get
			{
				return _CashoutTransaction;
			}
		}

		private readonly ICoin _EscrowedCoin;
		public ICoin EscrowedCoin
		{
			get
			{
				return _EscrowedCoin;
			}
		}

		public uint256 GetSignatureHash()
		{
			return Transaction.GetSignatureHash(EscrowedCoin);
		}
	}
}
