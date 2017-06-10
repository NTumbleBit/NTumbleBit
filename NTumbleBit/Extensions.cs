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

		public static JArray ListTransactions(this RPCClient rpcClient)
		{
			JArray array = new JArray();
			int count = 100;
			int skip = 0;
			int highestConfirmation = 0;

			while(true)
			{
				var result = rpcClient.SendCommandNoThrows("listtransactions", "*", count, skip, true);
				skip += count;
				if(result.Error != null)
					return null;
				var transactions = (JArray)result.Result;
				foreach(var obj in transactions)
				{
					array.Add(obj);
					if(obj["confirmations"] != null)
					{
						highestConfirmation = Math.Max(highestConfirmation, (int)obj["confirmations"]);
					}
				}
				if(transactions.Count < count || highestConfirmation >= 1400)
					break;
			}
			return array;
		}
	}
}
