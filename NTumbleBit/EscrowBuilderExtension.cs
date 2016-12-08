using NBitcoin.BuilderExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

namespace NTumbleBit
{
	public class EscrowBuilderExtension : BuilderExtension
	{
		public override bool CanCombineScriptSig(Script scriptPubKey, Script a, Script b)
		{
			return EscrowScriptBuilder.ExtractEscrowScriptPubKeyParameters(scriptPubKey) != null;
		}

		public override bool CanDeduceScriptPubKey(Script scriptSig)
		{
			return false;
		}

		public override bool CanEstimateScriptSigSize(Script scriptPubKey)
		{
			return EscrowScriptBuilder.ExtractEscrowScriptPubKeyParameters(scriptPubKey) != null;
		}

		public override bool CanGenerateScriptSig(Script scriptPubKey)
		{
			return EscrowScriptBuilder.ExtractEscrowScriptPubKeyParameters(scriptPubKey) != null;
		}

		public override Script CombineScriptSig(Script scriptPubKey, Script a, Script b)
		{
			var para = EscrowScriptBuilder.ExtractEscrowScriptPubKeyParameters(scriptPubKey);
			// Combine all the signatures we've got:
			var aSigs = EscrowScriptBuilder.ExtractScriptSigParameters(a);
			if(aSigs == null || aSigs.Length != 2)
				return b;
			var bSigs = EscrowScriptBuilder.ExtractScriptSigParameters(b);
			if(bSigs == null || bSigs.Length != 2)
				return a;
			int sigCount = 0;
			TransactionSignature[] sigs = new TransactionSignature[2];
			for(int i = 0; i < 2; i++)
			{
				var aSig = i < aSigs.Length ? aSigs[i] : null;
				var bSig = i < bSigs.Length ? bSigs[i] : null;
				var sig = aSig ?? bSig;
				if(sig != null)
				{
					sigs[i] = sig;
					sigCount++;
				}
				if(sigCount == 2)
					break;
			}
			if(sigCount == 2)
				sigs = sigs.Where(s => s != null && s != TransactionSignature.Empty).ToArray();
			return EscrowScriptBuilder.GenerateScriptSig(sigs);
		}

		public override Script DeduceScriptPubKey(Script scriptSig)
		{
			throw new NotImplementedException();
		}

		public override int EstimateScriptSigSize(Script scriptPubKey)
		{
			var p2mk = EscrowScriptBuilder.ExtractEscrowScriptPubKeyParameters(scriptPubKey);
			return EscrowScriptBuilder.GenerateScriptSig(Enumerable.Range(0, 2).Select(o => DummySignature).ToArray()).Length;
		}

		public override Script GenerateScriptSig(Script scriptPubKey, IKeyRepository keyRepo, ISigner signer)
		{
			var multiSigParams = EscrowScriptBuilder.ExtractEscrowScriptPubKeyParameters(scriptPubKey);
			TransactionSignature[] signatures = new TransactionSignature[2];
			var keys =
				multiSigParams
				.EscrowKeys
				.Select(p => keyRepo.FindKey(p.ScriptPubKey))
				.ToArray();

			int sigCount = 0;
			for(int i = 0; i < keys.Length; i++)
			{
				if(sigCount == 2)
					break;
				if(keys[i] != null)
				{
					var sig = signer.Sign(keys[i]);
					signatures[i] = sig;
					sigCount++;
				}
			}

			IEnumerable<TransactionSignature> sigs = signatures;
			if(sigCount == 2)
			{
				sigs = sigs.Where(s => s != TransactionSignature.Empty && s != null);
			}
			return EscrowScriptBuilder.GenerateScriptSig(sigs.ToArray());
		}
	}
}
