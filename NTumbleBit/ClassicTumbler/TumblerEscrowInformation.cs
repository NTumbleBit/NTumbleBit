using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

namespace NTumbleBit.ClassicTumbler
{
	public class TumblerEscrowInformation
	{
		public PubKey EscrowKey
		{
			get;
			set;
		}
		public PubKey RedeemKey
		{
			get;
			set;
		}
		public Transaction Transaction
		{
			get; set;
		}
	}
}
