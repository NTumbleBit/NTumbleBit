using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json.Linq;


#if !CLIENT
namespace NTumbleBit.TumblerServer.Services.RPCServices
#else
namespace NTumbleBit.Client.Tumbler.Services.RPCServices
#endif
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

		public TransactionInformation[] GetTransactions(Script scriptPubKey)
		{
			if(scriptPubKey == null)
				throw new ArgumentNullException("scriptPubKey");

			var address = scriptPubKey.GetDestinationAddress(RPCClient.Network);
			if(address == null)
				return new TransactionInformation[0];


			var result = RPCClient.SendCommand("listtransactions", "", 100, 0, true);
			if(result.Error != null)
				return null;


			var transactions = (JArray)result.Result;
			List<TransactionInformation> results = new List<TransactionInformation>();
			foreach(var obj in transactions)
			{
				var txId = new uint256((string)obj["txid"]);
				var tx = GetTransaction(txId);
				if((string)obj["address"] == address.ToString())
				{
					results.Add(tx);
				}
				else
				{
					foreach(var input in tx.Transaction.Inputs)
					{
						var p2shSig = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(input.ScriptSig);
						if(p2shSig != null)
						{
							if(p2shSig.RedeemScript.Hash.ScriptPubKey == address.ScriptPubKey)
							{
								results.Add(tx);
							}
						}
					}
				}
			}
			return results.ToArray();
		}

		public TransactionInformation GetTransaction(uint256 txId)
		{
			try
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
			catch(RPCException) { return null; }
		}

		public void Track(Script scriptPubkey)
		{
			var address = scriptPubkey.GetDestinationAddress(RPCClient.Network);
			if(address != null)
				RPCClient.ImportAddress(address, "", false);
		}
	}
}
