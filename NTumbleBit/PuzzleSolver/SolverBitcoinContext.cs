using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
	public class PaymentCashoutContext
	{
		public PaymentCashoutContext()
		{

		}

		public PubKey RedeemKey
		{
			get; set;
		}

		public PubKey RefundKey
		{
			get; set;
		}

		public LockTime Expiration
		{
			get; set;
		}


		public Script CreateOfferScript(uint160[] hashes)
		{
			var refund = SolverScriptBuilder.CreateRefundScript(Expiration, RefundKey);
			return SolverScriptBuilder.CreateOfferScript(hashes, RedeemKey, refund);
		}

		public Script CreateFulfillScript(TransactionSignature signature, SolutionKey[] solutionKey)
		{
			var offerScript = CreateOfferScript(solutionKey.Select(s => s.GetHash()).ToArray());
			return SolverScriptBuilder.GetFulfillScript(signature, solutionKey, offerScript);
		}
	}
}
