using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.TumblerServer.Models
{
    public class ChannelToClientRequest
    {
		public PubKey EscrowKey
		{
			get; set;
		}
	}

	public class ChannelToClientResponse
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
}
