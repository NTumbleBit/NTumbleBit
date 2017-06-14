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
		public class TransactionsCache
		{
			internal JArray Transactions;
		}

		public RPCBlockExplorerService(RPCClient client, IRepository repo)
		{
			if(client == null)
				throw new ArgumentNullException(nameof(client));
			if(repo == null)
				throw new ArgumentNullException("repo");
			_RPCClient = client;
			_Repo = repo;
		}

		IRepository _Repo;
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
			TransactionsCache cache = null;
			return GetTransactions(ref cache, scriptPubKey, withProof);
		}

		public TransactionInformation[] GetTransactions(ref TransactionsCache cache, Script scriptPubKey, bool withProof)
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
			{
				var walletTransactions = cache?.Transactions ?? RPCClient.ListTransactions();
				if(cache == null)
					cache = new TransactionsCache() { Transactions = walletTransactions };
				results = Filter(walletTransactions, !withProof, address);
			}
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

		private List<TransactionInformation> Filter(JArray transactions, bool includeUnconf, BitcoinAddress address)
		{
			List<TransactionInformation> results = new List<TransactionInformation>();
			HashSet<uint256> resultsSet = new HashSet<uint256>();
			foreach(var obj in transactions)
			{
				var txId = new uint256((string)obj["txid"]);

				//Core does not show the address for outgoing transactions so we can't use the following:
				//if((string)obj["address"] == address.ToString())


				//May have duplicates
				if(!resultsSet.Contains(txId))
				{
					var confirmations = Math.Max(0, obj["confirmations"] == null ? 0 : (int)obj["confirmations"]);

					var tx = GetCachedTransaction(txId, confirmations);
					if(tx == null)
					{
						tx = GetTransaction(txId);
						if(tx != null)
							PutCached(txId, tx);
					}

					if(tx == null || (!includeUnconf && tx.Confirmations == 0))
						continue;

					if(tx.Transaction.Outputs.Any(o => o.ScriptPubKey == address.ScriptPubKey) ||
					   tx.Transaction.Inputs.Any(o => o.ScriptSig.GetSigner().ScriptPubKey == address.ScriptPubKey))
					{

						resultsSet.Add(txId);
						results.Add(tx);
					}
				}
			}
			return results;
		}

		private void PutCached(uint256 txId, TransactionInformation tx)
		{
			_Repo.UpdateOrInsert("CachedTransactions", txId.ToString(), tx.Transaction, (a, b) => b);
		}

		private TransactionInformation GetCachedTransaction(uint256 txId, int confirmations)
		{
			var tx = _Repo.Get<Transaction>("CachedTransactions", txId.ToString());
			if(tx == null)
				return null;
			return new TransactionInformation()
			{
				Transaction = tx,
				Confirmations = confirmations
			};
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

		public void Track(Script scriptPubkey)
		{
			var address = scriptPubkey.GetDestinationAddress(RPCClient.Network);
			if(address != null)
			{
				RPCClient.ImportAddress(address, "", false);
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
