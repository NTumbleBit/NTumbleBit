using NBitcoin;
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

		public static ScriptCoin Clone(this ScriptCoin scriptCoin)
		{
			return new ScriptCoin(scriptCoin, scriptCoin.Redeem);
		}

		public static async Task<TResult> WithTimeout<TResult>(this Task<TResult> task, TimeSpan timeout)
		{
			if (task == await Task.WhenAny(task, Task.Delay(timeout)))
			{
				return await task;
			}
			throw new TimeoutException();
		}
	}
}
