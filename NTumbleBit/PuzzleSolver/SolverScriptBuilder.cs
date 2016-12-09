using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
	public static class SolverScriptBuilder
	{		

		public static Script CreateOfferScript(uint160[] hashes, PubKey fullfillKey, PubKey redeemKey, LockTime expiration)
		{
			if(hashes == null)
				throw new ArgumentNullException("hashes");
			if(fullfillKey == null)
				throw new ArgumentNullException("fullfillKey");
			if(redeemKey == null)
				throw new ArgumentNullException("redeemKey");
			List<Op> ops = new List<Op>();
			ops.Add(OpcodeType.OP_DEPTH);
			ops.Add(Op.GetPushOp(hashes.Length + 1));
			ops.Add(OpcodeType.OP_EQUAL);
			ops.Add(OpcodeType.OP_IF);
			foreach(var hash in hashes)
			{
				ops.Add(OpcodeType.OP_RIPEMD160);
				ops.Add(Op.GetPushOp(hash.ToBytes()));
				ops.Add(OpcodeType.OP_EQUALVERIFY);
			}
			ops.Add(Op.GetPushOp(fullfillKey.ToBytes()));
			ops.Add(OpcodeType.OP_ELSE);
			ops.Add(Op.GetPushOp(expiration));
			ops.Add(OpcodeType.OP_CHECKLOCKTIMEVERIFY);
			ops.Add(OpcodeType.OP_DROP);
			ops.Add(Op.GetPushOp(redeemKey.ToBytes()));
			ops.Add(OpcodeType.OP_ENDIF);
			ops.Add(OpcodeType.OP_CHECKSIG);
			return new Script(ops.ToArray());
		}
		public static SolutionKey[] ExtractSolutions(Script scriptSig, int expectedSolutions)
		{
			if(scriptSig == null)
				throw new ArgumentNullException("scriptSig");
			var ops = scriptSig.ToOps().ToArray();
			if(ops.Length != expectedSolutions + 2)
				return null;
			return ops.Skip(1).Take(expectedSolutions).Select(o => new SolutionKey(o.PushData)).Reverse().ToArray();
		}

		public static Script CreateFulfillScript(TransactionSignature signature, SolutionKey[] keys)
		{
			if(signature == null)
				throw new ArgumentNullException("signature");
			if(keys == null)
				throw new ArgumentNullException("keys");
			List<Op> ops = new List<Op>();
			ops.Add(Op.GetPushOp(signature.ToBytes()));
			foreach(var key in keys.Reverse())
			{
				ops.Add(Op.GetPushOp(key.ToBytes(true)));
			}
			return new Script(ops.ToList());
		}
	}
}
