using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json.Linq;
using NBitcoin.DataEncoders;


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

		public TransactionInformation[] GetTransactions(Script scriptPubKey, bool withProof)
		{
			if(scriptPubKey == null)
				throw new ArgumentNullException("scriptPubKey");

			var address = scriptPubKey.GetDestinationAddress(RPCClient.Network);
			if(address == null)
				return new TransactionInformation[0];

			List<TransactionInformation> results = new List<TransactionInformation>();
			int count = 10;
			int skip = 0;
			int highestConfirmation = 0;
			while(true)
			{
				var result = RPCClient.SendCommandNoThrows("listtransactions", "", count, skip, true);
				skip += count;
				if(result.Error != null)
					return null;
				var transactions = (JArray)result.Result;
				foreach(var obj in transactions)
				{
					var txId = new uint256((string)obj["txid"]);

					if(obj["confirmations"] != null)
					{
						highestConfirmation = Math.Max(highestConfirmation, (int)obj["confirmations"]);
					}

					var tx = GetTransaction(txId, withProof);
					if(tx == null)
						continue;
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
				if(transactions.Count < count || highestConfirmation >= 1000)
					break;
			}
			return results.ToArray();
		}

		public TransactionInformation GetTransaction(uint256 txId, bool withProof)
		{
			try
			{
				//check in the txindex
				var result = RPCClient.SendCommandNoThrows("getrawtransaction", txId.ToString(), 1);
				if(result == null || result.Error != null)
				{
					//check in the wallet tx
					result = RPCClient.SendCommandNoThrows("gettransaction", txId.ToString(), true);
					if(result == null || result.Error != null)
						return null;
				}
				var tx = new Transaction((string)result.Result["hex"]);
				var confirmations = result.Result["confirmations"];
				var confCount = confirmations == null ? 0 : Math.Max(0, (int)confirmations);

				MerkleBlock proof = null;
				if(withProof)
				{
					if(confCount == 0)
						return null;
					result = RPCClient.SendCommandNoThrows("gettxoutproof", new JArray(txId.ToString()));
					if(result == null || result.Error != null)
						return null;
					proof = new MerkleBlock();
					proof.ReadWrite(Encoders.Hex.DecodeData(result.ResultString));
				}
				return new TransactionInformation()
				{
					Confirmations = confCount,
					Transaction = tx,
					MerkleProof = proof
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

		public int GetBlockConfirmations(uint256 blockId)
		{
			var result = RPCClient.SendCommandNoThrows("getblock", blockId.ToString(), true);
			if(result == null || result.Error != null)
				return 0;
			return (int)result.Result["confirmations"];
		}

		public bool TrackPrunedTransaction(Transaction transaction, MerkleBlock merkleProof)
		{
			var result = RPCClient.SendCommandNoThrows("importprunedfunds", transaction.ToHex(), Encoders.Hex.EncodeData(merkleProof.ToBytes()));
			return result != null && result.Error == null;
		}
	}
}
