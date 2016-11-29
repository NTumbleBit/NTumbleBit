using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.TumblerServer.Models
{
    public class ChannelToTumblerRequest
    {
		public PubKey EscrowKey
		{
			get; set;
		}
		public PubKey RedeemKey
		{
			get; set;
		}
	}
	public class ChannelToTumblerResponse
	{
		public PubKey EscrowKey
		{
			get; set;
		}
	}
}
