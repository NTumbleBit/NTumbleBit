using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
	public class OfferScriptPubKeyParameters
	{		
		public uint160[] Hashes
		{
			get; set;
		}
		public PubKey FulfillKey
		{
			get; set;
		}
		public PubKey RedeemKey
		{
			get; set;
		}
		public LockTime Expiration
		{
			get; set;
		}

		public Script ToScript()
		{
			List<Op> ops = new List<Op>();
			ops.Add(OpcodeType.OP_DEPTH);
			ops.Add(Op.GetPushOp(Hashes.Length + 1));
			ops.Add(OpcodeType.OP_EQUAL);
			ops.Add(OpcodeType.OP_IF);
			{
				foreach(var hash in Hashes)
				{
					ops.Add(OpcodeType.OP_RIPEMD160);
					ops.Add(Op.GetPushOp(hash.ToBytes()));
					ops.Add(OpcodeType.OP_EQUALVERIFY);
				}
				ops.Add(Op.GetPushOp(FulfillKey.ToBytes()));
			}
			ops.Add(OpcodeType.OP_ELSE);
			{

				ops.Add(Op.GetPushOp(Expiration));
				ops.Add(OpcodeType.OP_CHECKLOCKTIMEVERIFY);
				ops.Add(OpcodeType.OP_DROP);
				ops.Add(Op.GetPushOp(RedeemKey.ToBytes()));
			}
			ops.Add(OpcodeType.OP_ENDIF);
			ops.Add(OpcodeType.OP_CHECKSIG);
			return new Script(ops.ToArray());
		}
	}
	public static class SolverScriptBuilder
	{
		
		public static SolutionKey[] ExtractSolutions(Script scriptSig, int expectedSolutions)
		{
			if(scriptSig == null)
				throw new ArgumentNullException(nameof(scriptSig));
			var ops = scriptSig.ToOps().ToArray();
			if(ops.Length != expectedSolutions + 2)
				return null;
			return ops.Skip(1).Take(expectedSolutions).Select(o => new SolutionKey(o.PushData)).Reverse().ToArray();
		}

		public static Script CreateFulfillScript(TransactionSignature signature, SolutionKey[] keys)
		{
			if(keys == null)
				throw new ArgumentNullException(nameof(keys));
			List<Op> ops = new List<Op>();
			ops.Add(signature == null ? Op.GetPushOp(TrustedBroadcastRequest.PlaceholderSignature) : Op.GetPushOp(signature.ToBytes()));
			foreach(var key in keys.Reverse())
			{
				ops.Add(Op.GetPushOp(key.ToBytes(true)));
			}
			return new Script(ops.ToList());
		}
	}
}
