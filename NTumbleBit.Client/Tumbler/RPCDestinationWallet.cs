using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

namespace NTumbleBit.Client.Tumbler
{
    public class RPCDestinationWallet : IDestinationWallet
    {
		RPCClient _RPC;
		public RPCDestinationWallet(RPCClient client)
		{
			_RPC = client;
		}

		public Script GetNewDestination()
		{
			return _RPC.GetNewAddress().ScriptPubKey;
		}
	}
}
