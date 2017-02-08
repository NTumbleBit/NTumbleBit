﻿using NBitcoin.RPC;
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
				throw new ArgumentNullException(nameof(client));
			_RPCClient = client;
		}

		private readonly RPCClient _RPCClient;
		private bool supportReceivedByAddress = true;

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
				throw new ArgumentNullException(nameof(scriptPubKey));

			var address = scriptPubKey.GetDestinationAddress(RPCClient.Network);
			if(address == null)
				return new TransactionInformation[0];

			List<TransactionInformation> results = null;
			if(supportReceivedByAddress)
			{
				try
				{
					results = QueryWithListReceivedByAddress(withProof, address);
				}
				catch(RPCException)
				{
					supportReceivedByAddress = false;
				}
			}

			if(results == null)
				results = QueryWithListTransactions(withProof, address);
			if(withProof)
			{
				foreach(var tx in results.ToList())
				{
					MerkleBlock proof = null;
					var result = RPCClient.SendCommandNoThrows("gettxoutproof", new JArray(tx.Transaction.GetHash().ToString()));
					if(result == null || result.Error != null)
					{
						results.Remove(tx);
						continue;
					}
					proof = new MerkleBlock();
					proof.ReadWrite(Encoders.Hex.DecodeData(result.ResultString));
					tx.MerkleProof = proof;
				}
			}
			return results.ToArray();
		}

		private List<TransactionInformation> QueryWithListReceivedByAddress(bool withProof, BitcoinAddress address)
		{
			var result = RPCClient.SendCommand("listreceivedbyaddress", 0, false, true, address.ToString());
			var transactions = ((JArray)result.Result).OfType<JObject>().Select(o => o["txids"]).OfType<JArray>().SingleOrDefault();
			if(transactions == null)
				return null;

			HashSet<uint256> resultsSet = new HashSet<uint256>();
			List<TransactionInformation> results = new List<TransactionInformation>();
			foreach(var txIdObj in transactions)
			{
				var txId = new uint256(txIdObj.ToString());
				//May have duplicates
				if(!resultsSet.Contains(txId))
				{
					var tx = GetTransaction(txId);
					if(tx == null || (withProof && tx.Confirmations == 0))
						continue;
					resultsSet.Add(txId);
					results.Add(tx);
				}
			}
			return results;
		}

		private List<TransactionInformation> QueryWithListTransactions(bool withProof, BitcoinAddress address)
		{
			List<TransactionInformation> results = new List<TransactionInformation>();
			HashSet<uint256> resultsSet = new HashSet<uint256>();
			int count = 100;
			int skip = 0;
			int highestConfirmation = 0;
			while(true)
			{
				var result = RPCClient.SendCommandNoThrows("listtransactions", "*", count, skip, true);
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

					if((string)obj["address"] == address.ToString())
					{
						//May have duplicates
						if(!resultsSet.Contains(txId))
						{
							var tx = GetTransaction(txId);
							if(tx == null || (withProof && tx.Confirmations == 0))
								continue;
							resultsSet.Add(txId);
							results.Add(tx);
						}
					}
				}
				if(transactions.Count < count || highestConfirmation >= 1000)
					break;
			}
			return results;
		}

		public TransactionInformation GetTransaction(uint256 txId)
		{
			try
			{
				//check in the wallet tx
				var result = RPCClient.SendCommandNoThrows("gettransaction", txId.ToString(), true);
				if(result == null || result.Error != null)
				{
					//check in the txindex
					result = RPCClient.SendCommandNoThrows("getrawtransaction", txId.ToString(), 1);
					if(result == null || result.Error != null)
						return null;
				}
				var tx = new Transaction((string)result.Result["hex"]);
				var confirmations = result.Result["confirmations"];
				var confCount = confirmations == null ? 0 : Math.Max(0, (int)confirmations);

				return new TransactionInformation
				{
					Confirmations = confCount,
					Transaction = tx
				};
			}
			catch(RPCException) { return null; }
		}

		public void Track(string label, Script scriptPubkey)
		{
			var address = scriptPubkey.GetDestinationAddress(RPCClient.Network);
			if(address != null)
			{
				RPCClient.ImportAddress(address, label, false);
			}
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
