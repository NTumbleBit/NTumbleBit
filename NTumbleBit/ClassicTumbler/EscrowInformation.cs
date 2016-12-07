using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
    public class EscrowInformation
    {
		public PubKey OurEscrowKey
		{
			get; set;
		}
		public PubKey OtherEscrowKey
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

		public Script CreateEscrow()
		{
			return EscrowScriptBuilder.CreateEscrow(new PubKey[] { OurEscrowKey, OtherEscrowKey }, RedeemKey, LockTime);
		}
	}
}
