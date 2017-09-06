using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;

namespace NTumbleBit.Services.RPC
{
	public class RPCBatch
	{
		public RPCBatch(RPCClient client)
		{
			BatchInterval = TimeSpan.FromSeconds(5);
			_Client = client;
		}
		public TimeSpan BatchInterval
		{
			get; set;
		}
		RPCClient _Client;
		RPCClient _CurrentBatch;
		object l = new object();
		public async Task<T> Do<T>(Func<RPCClient, Task<T>> act)
		{
			bool createdBatch = false;
			RPCClient batch = null;
			lock(l)
			{
				if(_CurrentBatch == null)
				{
					batch = _Client.PrepareBatch();
					_CurrentBatch = batch;
					createdBatch = true;
				}
				else
				{
					batch = _CurrentBatch;
					createdBatch = false;
				}
			}
			var request = act(batch);
			if(createdBatch)
			{
				await Task.Delay(BatchInterval).ConfigureAwait(false);
				await batch.SendBatchAsync().ConfigureAwait(false);
			}
			return await request.ConfigureAwait(false);
		}
	}
}
