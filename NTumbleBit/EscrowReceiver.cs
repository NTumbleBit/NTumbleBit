using NBitcoin;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit
{
    public class EscrowReceiver
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
		}

		protected State InternalState
		{
			get; set;
		}

		public string Id
		{
			get
			{
				return InternalState.EscrowedCoin.ScriptPubKey.ToHex();
			}
		}

		public virtual void ConfigureEscrowedCoin(ScriptCoin escrowedCoin, Key escrowKey)
		{			
			if(escrowedCoin == null)
				throw new ArgumentNullException("escrowedCoin");
			if(escrowKey == null)
				throw new ArgumentNullException("escrowKey");
			var redeem = EscrowScriptBuilder.ExtractEscrowScriptPubKeyParameters(escrowedCoin.Redeem);
			if(redeem == null)
				throw new PuzzleException("Invalid escrow");
			if(!redeem.EscrowKeys.Contains(escrowKey.PubKey))
				throw new PuzzleException("Invalid escrow");

			InternalState.EscrowKey = escrowKey;
			InternalState.EscrowedCoin = escrowedCoin;			
		}
	}
}
