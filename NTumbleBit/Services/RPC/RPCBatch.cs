using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using NBitcoin;
using NBitcoin.RPC;

namespace NTumbleBit.Services.RPC
{
	internal class RPCBatch<T> : BatchBase<Func<RPCClient, Task<T>>, T[]>
	{
		public RPCBatch(RPCClient client)
		{
			BatchInterval = TimeSpan.FromSeconds(5);
			_Client = client;
		}
		RPCClient _Client;
		protected override async Task<T[]> RunAsync(Func<RPCClient, Task<T>>[] data)
		{
			var batch = _Client.PrepareBatch();
			var tasks = data.Select(d => d(batch)).ToArray();
			await batch.SendBatchAsync().ConfigureAwait(false);
			await Task.WhenAll(tasks).ConfigureAwait(false);
			return tasks.Select(t => t.Result).ToArray();
		}
	}
}
