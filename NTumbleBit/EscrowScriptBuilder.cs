using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit
{
    public class EscrowScriptBuilder
    {
		public static Script CreateEscrow(PubKey[] keys, PubKey redeem, LockTime timeout)
		{
			keys = keys.OrderBy(o => o.ToHex()).ToArray();
			List<Op> ops = new List<Op>();
			ops.Add(OpcodeType.OP_IF);
			ops.Add(OpcodeType.OP_2);
			ops.Add(Op.GetPushOp(keys[0].ToBytes()));
			ops.Add(Op.GetPushOp(keys[1].ToBytes()));
			ops.Add(OpcodeType.OP_2);
			ops.Add(OpcodeType.OP_CHECKMULTISIG);
			ops.Add(OpcodeType.OP_ELSE);
			ops.Add(Op.GetPushOp(timeout.ToBytes()));
			ops.Add(OpcodeType.OP_CHECKLOCKTIMEVERIFY);
			ops.Add(OpcodeType.OP_DROP);
			ops.Add(Op.GetPushOp(redeem.ToBytes()));
			ops.Add(OpcodeType.OP_CHECKSIG);
			ops.Add(OpcodeType.OP_ENDIF);
			return new Script(ops.ToArray());
		}
	}
}
