using NBitcoin;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NTumbleBit.ClassicTumbler;

namespace NTumbleBit
{
	public interface IEscrow
	{
		ScriptCoin EscrowedCoin
		{
			get;
		}
		LockTime GetLockTime(CycleParameters cycle);
	}
    public abstract class EscrowInitiator : IEscrow
    {
		public class State
		{
			public ScriptCoin EscrowedCoin
			{
				get;
				set;
			}
			public Key EscrowKey
			{
				get;
				set;
			}
			public Key RedeemKey
			{
				get;
				set;
			}
		}

		protected State InternalState
		{
			get; set;
		}

		public virtual void ConfigureEscrowedCoin(ScriptCoin escrowedCoin, Key escrowKey, Key redeemKey)
		{
			if(escrowedCoin == null)
				throw new ArgumentNullException("escrowedCoin");
			if(escrowKey == null)
				throw new ArgumentNullException("escrowKey");
			if(redeemKey == null)
				throw new ArgumentNullException("redeemKey");
			var escrow = EscrowScriptBuilder.ExtractEscrowScriptPubKeyParameters(escrowedCoin.Redeem);
			if(escrow == null || !escrow.EscrowKeys.Any(e => e == escrowKey.PubKey))
				throw new PuzzleException("Invalid escrow");
			InternalState.EscrowedCoin = escrowedCoin;
			InternalState.EscrowKey = escrowKey;
			InternalState.RedeemKey = redeemKey;			
		}

		public Transaction CreateRedeemTransaction(FeeRate feeRate, Script redeemDestination)
		{
			if(feeRate == null)
				throw new ArgumentNullException("feeRate");

			var coin = InternalState.EscrowedCoin;
			var escrow = EscrowScriptBuilder.ExtractEscrowScriptPubKeyParameters(coin.Redeem);

			Transaction tx = new Transaction();
			tx.LockTime = escrow.LockTime;
			tx.Inputs.Add(new TxIn(coin.Outpoint));
			tx.Inputs[0].Sequence = 0;
			tx.Outputs.Add(new TxOut(coin.Amount, redeemDestination));
			var vSize = tx.GetVirtualSize() + 80;
			tx.Outputs[0].Value -= feeRate.GetFee(vSize);

			var hash = tx.GetSignatureHash(coin);
			var sig = tx.SignInput(InternalState.RedeemKey, coin);
			tx.Inputs[0].ScriptSig = EscrowScriptBuilder.GenerateScriptSig(new[] { sig }) + Op.GetPushOp(coin.Redeem.ToBytes());

			return tx;
		}

		public abstract LockTime GetLockTime(CycleParameters cycle);

		public string Id
		{
			get
			{
				return InternalState.EscrowedCoin.ScriptPubKey.ToHex();
			}
		}

		public ScriptCoin EscrowedCoin
		{
			get
			{
				return InternalState.EscrowedCoin;
			}
		}
	}
}
