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
		public static readonly byte[] PlaceholderSignature = new byte[71];

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
		
		public bool IsBroadcastableAt(int height)
		{
			return height >= BroadcastAt.Height && Transaction.IsFinal(DateTimeOffset.UtcNow, height + 1);
		}

		public Transaction ReSign(Coin coin)
		{
			var transaction = Transaction.Clone();
			transaction.Inputs[0].PrevOut = coin.Outpoint;
			var redeem = new Script(transaction.Inputs[0].ScriptSig.ToOps().Last().PushData);
			var scriptCoin = coin.ToScriptCoin(redeem);
			byte[] signature = transaction.SignInput(Key, scriptCoin).ToBytes();
			List<Op> resignedScriptSig = new List<Op>();
			foreach(var op in transaction.Inputs[0].ScriptSig.ToOps())
			{
				resignedScriptSig.Add(IsPlaceholder(op) ? Op.GetPushOp(signature) : op);
			}
			transaction.Inputs[0].ScriptSig = new Script(resignedScriptSig.ToArray());
			return transaction;
		}

		private static bool IsPlaceholder(Op op)
		{
			return op.PushData != null && op.PushData.SequenceEqual(PlaceholderSignature);
		}
	}
}
