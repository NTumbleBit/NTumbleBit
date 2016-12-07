using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

namespace NTumbleBit.TumblerServer.Services.RPCServices
{
	public class RPCBlockExplorerService : IBlockExplorerService
	{
		public RPCBlockExplorerService(RPCClient client)
		{
			if(client == null)
				throw new ArgumentNullException("client");
			_RPCClient = client;
		}

		private readonly RPCClient _RPCClient;
		public RPCClient RPCClient
		{
			get
			{
				return _RPCClient;
			}
		}
		public int GetCurrentHeight()
		{
			return RPCClient.GetBlockCount();
		}

		public TransactionInformation GetTransaction(uint256 txId)
		{
			var result = RPCClient.SendCommand("getrawtransaction", txId.ToString(), 1);
			if(result == null || result.Error != null)
				return null;
			var tx = new Transaction((string)result.Result["hex"]);
			var confirmations = result.Result["confirmations"];
			return new TransactionInformation()
			{
				Confirmations = confirmations == null ? 0 : (int)confirmations,
				Transaction = tx
			};
		}

		public void Track(Script scriptPubkey)
		{
			var address = scriptPubkey.GetDestinationAddress(RPCClient.Network);
			if(address != null)
				RPCClient.ImportAddress(address, "", false);
		}
	}
}
