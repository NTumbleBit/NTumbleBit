using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public static class Extensions
	{
		public static RPCResponse SendCommandNoThrows(this RPCClient client, string commandName, params object[] parameters)
		{
			return client.SendCommand(new RPCRequest
			{
				Method = commandName,
				Params = parameters
			}, throwIfRPCError: false);
		}
	}
}
