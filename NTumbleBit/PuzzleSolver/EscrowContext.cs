using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
	public class EscrowContext
	{
		public EscrowContext()
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


		public Script CreateEscrowScript(uint160[] hashes)
		{
			var refund = SolverScriptBuilder.CreateRefundScript(Expiration, RefundKey);
			return SolverScriptBuilder.CreateEscrowScript(hashes, RedeemKey, refund);
		}

		public Script CreateEscrowCashout(EscrowContext escrowContext, TransactionSignature signature, SolutionKey[] solutionKey)
		{
			var redeem = CreateEscrowScript(solutionKey.Select(s => s.GetHash()).ToArray());
			return SolverScriptBuilder.GetCashoutScript(signature, solutionKey, redeem);
		}
	}
}
