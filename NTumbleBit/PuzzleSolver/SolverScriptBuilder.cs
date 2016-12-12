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
		public PubKey FullfillKey
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
	}
	public static class SolverScriptBuilder
	{		

		public static Script CreateOfferScript(OfferScriptPubKeyParameters parameters)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			List<Op> ops = new List<Op>();
			ops.Add(OpcodeType.OP_DEPTH);
			ops.Add(Op.GetPushOp(parameters.Hashes.Length + 1));
			ops.Add(OpcodeType.OP_EQUAL);
			ops.Add(OpcodeType.OP_IF);
			foreach(var hash in parameters.Hashes)
			{
				ops.Add(OpcodeType.OP_RIPEMD160);
				ops.Add(Op.GetPushOp(hash.ToBytes()));
				ops.Add(OpcodeType.OP_EQUALVERIFY);
			}
			ops.Add(Op.GetPushOp(parameters.FullfillKey.ToBytes()));
			ops.Add(OpcodeType.OP_ELSE);
			ops.Add(Op.GetPushOp(parameters.Expiration));
			ops.Add(OpcodeType.OP_CHECKLOCKTIMEVERIFY);
			ops.Add(OpcodeType.OP_DROP);
			ops.Add(Op.GetPushOp(parameters.RedeemKey.ToBytes()));
			ops.Add(OpcodeType.OP_ENDIF);
			ops.Add(OpcodeType.OP_CHECKSIG);
			return new Script(ops.ToArray());
		}

		public static OfferScriptPubKeyParameters ExtractOfferScriptParameters(Script scriptPubKey)
		{
			if(scriptPubKey == null)
				throw new ArgumentNullException("scriptPubKey");
			try
			{

				var result = new OfferScriptPubKeyParameters();
				var ops = scriptPubKey.ToOps().ToArray();
				int i = 0;
				if(ops[i++].Code != OpcodeType.OP_DEPTH)
					return null;
				var pushCount = ops[i++].GetInt();
				if(pushCount == null || pushCount > 500)
					return null;
				result.Hashes = new uint160[pushCount.Value - 1];
				CheckMinimal(ops[i - 1], pushCount.Value);
				if(ops[i++].Code != OpcodeType.OP_EQUAL)
					return null;
				if(ops[i++].Code != OpcodeType.OP_IF)
					return null;
				for(int y = 0; y < result.Hashes.Length; y++)
				{
					if(ops[i++].Code != OpcodeType.OP_RIPEMD160)
						return null;
					result.Hashes[y] = new uint160(ops[i++].PushData);
					if(ops[i++].Code != OpcodeType.OP_EQUALVERIFY)
						return null;
				}
				result.FullfillKey = new PubKey(ops[i++].PushData);
				if(ops[i++].Code != OpcodeType.OP_ELSE)
					return null;
				result.Expiration = new LockTime(checked((uint)ops[i++].GetLong()));
				CheckMinimal(ops[i - 1], (uint)result.Expiration);
				if(ops[i++].Code != OpcodeType.OP_CHECKLOCKTIMEVERIFY)
					return null;
				if(ops[i++].Code != OpcodeType.OP_DROP)
					return null;
				result.RedeemKey = new PubKey(ops[i++].PushData);				
				if(ops[i++].Code != OpcodeType.OP_ENDIF)
					return null;
				if(ops[i++].Code != OpcodeType.OP_CHECKSIG)
					return null;
				if(i != ops.Length)
					return null;
				return result;
			}
			catch { return null; }
		}

		private static void CheckMinimal(Op op, long value)
		{
			if(!op.PushData.SequenceEqual(Op.GetPushOp(value).PushData))
				throw new Exception("Non minimal push");
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
			if(keys == null)
				throw new ArgumentNullException("keys");
			List<Op> ops = new List<Op>();
			ops.Add(signature == null ? OpcodeType.OP_0 : Op.GetPushOp(signature.ToBytes()));
			foreach(var key in keys.Reverse())
			{
				ops.Add(Op.GetPushOp(key.ToBytes(true)));
			}
			return new Script(ops.ToList());
		}
	}
}
