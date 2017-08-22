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

			/// <summary>
			/// Identify the channel to the tumbler
			/// </summary>
			public uint160 ChannelId
			{
				get;
				set;
			}
		}

		protected State InternalState
		{
			get; set;
		}

		public uint160 Id
		{
			get
			{
				return InternalState.ChannelId;
			}
		}

		public void SetChannelId(uint160 channelId)
		{
			if(channelId == null)
				throw new ArgumentNullException(nameof(channelId));
			InternalState.ChannelId = channelId;
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
