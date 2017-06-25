using NBitcoin;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit
{
    public abstract class EscrowReceiver : IEscrow
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
				throw new ArgumentNullException(nameof(escrowedCoin));
			if(escrowKey == null)
				throw new ArgumentNullException(nameof(escrowKey));
						
			InternalState.EscrowKey = escrowKey;
			InternalState.EscrowedCoin = escrowedCoin;
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
