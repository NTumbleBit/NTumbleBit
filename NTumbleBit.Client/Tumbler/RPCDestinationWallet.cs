using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json.Linq;

namespace NTumbleBit.Client.Tumbler
{
    public class RPCDestinationWallet : IDestinationWallet
    {
		RPCClient _RPC;
		public RPCDestinationWallet(RPCClient client)
		{
			if(client == null)
				throw new ArgumentNullException("client");
			_RPC = client;
		}

		public KeyPath GetKeyPath(Script script)
		{
			var address = script.GetDestinationAddress(_RPC.Network);
			if(address == null)
				return null;
			var result = (JObject)_RPC.SendCommand(RPCOperations.validateaddress, address.ToString()).Result;
			if(result["hdkeypath"] == null)
				return null;
			return new KeyPath(result["hdkeypath"].Value<string>());
		}

		public Script GetNewDestination()
		{
			return _RPC.GetNewAddress().ScriptPubKey;
		}
	}
}
