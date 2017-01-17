using NBitcoin;
using NBitcoin.BuilderExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
    public class OfferBuilderExtension : BuilderExtension
    {
		public OfferBuilderExtension()
		{

		}

		public override bool CanCombineScriptSig(Script scriptPubKey, Script a, Script b)
		{
			var parameters = SolverScriptBuilder.ExtractOfferScriptParameters(scriptPubKey);

			var aCount = a.ToOps().Count();
			var bCount = b.ToOps().Count();
			int min = Math.Min(aCount, bCount);
			int max = Math.Max(aCount, bCount);
			return min == 1 && max > 1;
		}

		public override bool CanDeduceScriptPubKey(Script scriptSig)
		{
			return false;
		}

		public override bool CanEstimateScriptSigSize(Script scriptPubKey)
		{
			return SolverScriptBuilder.ExtractOfferScriptParameters(scriptPubKey) != null;
		}

		public override bool CanGenerateScriptSig(Script scriptPubKey)
		{
			return SolverScriptBuilder.ExtractOfferScriptParameters(scriptPubKey) != null;
		}

		public override Script CombineScriptSig(Script scriptPubKey, Script a, Script b)
		{
			Script oneSigScript = a;
			Script otherScript = b;
			if(b.ToOps().Count() == 1)
			{
				oneSigScript = b;
				otherScript = a;
			}
			List<Op> ops = new List<Op>();
			ops.AddRange(oneSigScript.ToOps());
			ops.AddRange(otherScript.ToOps().Skip(1));
			return new Script(ops.ToArray());
		}

		public override Script DeduceScriptPubKey(Script scriptSig)
		{
			throw new NotImplementedException();
		}

		public override int EstimateScriptSigSize(Script scriptPubKey)
		{
			var offer = SolverScriptBuilder.ExtractOfferScriptParameters(scriptPubKey);
			return BuilderExtension.DummySignature.ToBytes().Length + offer.Hashes.Length * (int)SolutionKey.KeySize;
		}

		public override Script GenerateScriptSig(Script scriptPubKey, IKeyRepository keyRepo, ISigner signer)
		{
			var offer = SolverScriptBuilder.ExtractOfferScriptParameters(scriptPubKey);
			var key = keyRepo.FindKey(offer.fulfillKey.ScriptPubKey);
			if(key == null)
				return null;
			var sig = signer.Sign(key);
			return new Script(Op.GetPushOp(sig.ToBytes()));
		}
	}
}
