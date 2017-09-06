using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace NTumbleBit
{
    public static class Witnessifier
    {
		public static void Witnessify(this TxIn txIn)
		{
			var redeem = new Script(txIn.ScriptSig.ToOps().Last().PushData);
			txIn.WitScript = new WitScript(txIn.ScriptSig);
			txIn.ScriptSig = new Script(Op.GetPushOp(redeem.WitHash.ScriptPubKey.ToBytes()));
		}
    }
}
