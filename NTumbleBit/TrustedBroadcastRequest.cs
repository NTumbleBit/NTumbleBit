using NBitcoin;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class TrustedBroadcastRequest
	{
		public Script PreviousScriptPubKey
		{
			get; set;
		}
		public Transaction Transaction
		{
			get; set;
		}
		public Key Key
		{
			get; set;
		}
		public LockTime BroadcastAt
		{
			get;
			set;
		}
		public Transaction ReSign(Coin coin)
		{
			var transaction = Transaction.Clone();
			transaction.Inputs[0].PrevOut = coin.Outpoint;
			TransactionBuilder builder = new TransactionBuilder();
			builder.Extensions.Add(new EscrowBuilderExtension());
			builder.Extensions.Add(new OfferBuilderExtension());
			builder.AddCoins(coin);
			builder.AddKeys(Key);
			builder.SignTransactionInPlace(transaction);
			return transaction;
		}
	}
}
