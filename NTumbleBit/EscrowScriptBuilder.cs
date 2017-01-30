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
			ops.Add(OpcodeType.OP_DEPTH);
			ops.Add(OpcodeType.OP_3);
			ops.Add(OpcodeType.OP_EQUAL);
			ops.Add(OpcodeType.OP_IF);
			ops.Add(OpcodeType.OP_2);
			ops.Add(Op.GetPushOp(keys[0].ToBytes()));
			ops.Add(Op.GetPushOp(keys[1].ToBytes()));
			ops.Add(OpcodeType.OP_2);
			ops.Add(OpcodeType.OP_CHECKMULTISIG);
			ops.Add(OpcodeType.OP_ELSE);
			ops.Add(Op.GetPushOp(timeout));
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
			if(ops.Length != 16)
				return null;
			try
			{
				if(ops[0].Code != OpcodeType.OP_DEPTH ||
					ops[1].Code != OpcodeType.OP_3 ||
					ops[2].Code != OpcodeType.OP_EQUAL ||
					ops[3].Code != OpcodeType.OP_IF ||
					ops[4].Code != OpcodeType.OP_2)
					return null;

				var k1 = new PubKey(ops[5].PushData);
				var k2 = new PubKey(ops[6].PushData);

				if(ops[7].Code != OpcodeType.OP_2 ||
					ops[8].Code != OpcodeType.OP_CHECKMULTISIG ||
					ops[9].Code != OpcodeType.OP_ELSE)
					return null;

				var timeout = new LockTime((uint)ops[10].GetLong().Value);

				if(ops[11].Code != OpcodeType.OP_CHECKLOCKTIMEVERIFY ||
					ops[12].Code != OpcodeType.OP_DROP)
					return null;

				var redeem = new PubKey(ops[13].PushData);

				if(ops[14].Code != OpcodeType.OP_CHECKSIG ||
					ops[15].Code != OpcodeType.OP_ENDIF)
					return null;
				var keys = new[] { k1, k2 };
				var orderedKeys = keys.OrderBy(o => o.ToHex()).ToArray();
				if(!keys.SequenceEqual(orderedKeys))
					return null;
				return new EscrowScriptPubKeyParameters
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

		public static TransactionSignature[] ExtractScriptSigParameters(Script scriptSig)
		{
			var ops = scriptSig.ToOps().ToArray();
			if(ops.Length == 3)
			{
				return PayToMultiSigTemplate.Instance.ExtractScriptSigParameters(scriptSig);
			}
			else if(ops.Length == 1)
			{
				var sig = new TransactionSignature[1];
				try
				{
					if(ops[0].Code != OpcodeType.OP_0)
						sig[0] = new TransactionSignature(ops[0].PushData);
				}
				catch { return null; }
				return sig;
			}
			else
				return null;
		}

		public static Script GenerateScriptSig(TransactionSignature[] signatures)
		{
			if(signatures.Length == 2)
			{
				return PayToMultiSigTemplate.Instance.GenerateScriptSig(signatures);
			}
			else if(signatures.Length == 1)
			{
				if(signatures[0] == null)
					return new Script(OpcodeType.OP_0);
				return PayToPubkeyTemplate.Instance.GenerateScriptSig(signatures[0]);
			}
			else
				throw new ArgumentException("Invalid signature count");
		}
	}
}
