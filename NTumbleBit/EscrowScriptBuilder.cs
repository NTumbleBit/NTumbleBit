using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class EscrowScriptPubKeyParameters
	{
		public PubKey[] EscrowKeys
		{
			get; set;
		}
		public PubKey RedeemKey
		{
			get; set;
		}
		public LockTime LockTime
		{
			get; set;
		}
	}
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
		public static EscrowScriptPubKeyParameters ExtractEscrowScriptPubKeyParameters(Script script)
		{
			var ops = script.ToOps().ToArray();
			if(ops.Length != 13)
				return null;
			try
			{
				if(ops[0].Code != OpcodeType.OP_IF ||
					ops[1].Code != OpcodeType.OP_2)
					return null;

				var k1 = new PubKey(ops[2].PushData);
				var k2 = new PubKey(ops[3].PushData);

				if(ops[4].Code != OpcodeType.OP_2 ||
					ops[5].Code != OpcodeType.OP_CHECKMULTISIG ||
					ops[6].Code != OpcodeType.OP_ELSE)
					return null;

				var timeout = new LockTime((uint)ops[7].GetLong().Value);

				if(ops[8].Code != OpcodeType.OP_CHECKLOCKTIMEVERIFY ||
					ops[9].Code != OpcodeType.OP_DROP)
					return null;

				var redeem = new PubKey(ops[10].PushData);

				if(ops[11].Code != OpcodeType.OP_CHECKSIG ||
					ops[12].Code != OpcodeType.OP_ENDIF)
					return null;
				var keys = new[] { k1, k2 };
				var orderedKeys = keys.OrderBy(o => o.ToHex()).ToArray();
				if(!keys.SequenceEqual(orderedKeys))
					return null;
				return new EscrowScriptPubKeyParameters()
				{
					EscrowKeys = new[] { k1, k2 },
					LockTime = timeout,
					RedeemKey = redeem
				};
			}
			catch
			{
				return null;
			}
		}
	}
}
