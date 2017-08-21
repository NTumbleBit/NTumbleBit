using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json.Linq;
using NBitcoin.DataEncoders;
using System.Threading;

namespace NTumbleBit.Services.RPC
{
	public class RPCBlockExplorerService : IBlockExplorerService
	{
		RPCWalletCache _Cache;
		RPCBatch _RPCBatch;
		public RPCBlockExplorerService(RPCClient client, RPCWalletCache cache, IRepository repo)
		{
			if(client == null)
				throw new ArgumentNullException(nameof(client));
			if(repo == null)
				throw new ArgumentNullException("repo");
			if(cache == null)
				throw new ArgumentNullException("cache");
			_RPCClient = client;
			_Repo = repo;
			_Cache = cache;
			_RPCBatch = new RPCBatch(client);
		}

		public TimeSpan BatchInterval
		{
			get
			{
				return _RPCBatch.BatchInterval;
			}
			set
			{
				_RPCBatch.BatchInterval = value;
			}
		}

		IRepository _Repo;
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
			return _Cache.BlockCount;
		}

		public uint256 WaitBlock(uint256 currentBlock, CancellationToken cancellation = default(CancellationToken))
		{
			while(true)
			{
				cancellation.ThrowIfCancellationRequested();
				var h = _RPCClient.GetBestBlockHash();
				if(h != currentBlock)
				{
					_Cache.Refresh(h);
					return h;
				}
				cancellation.WaitHandle.WaitOne(5000);
			}
		}

		public ICollection<TransactionInformation> GetTransactions(Script scriptPubKey, bool withProof)
		{
			if(scriptPubKey == null)
				throw new ArgumentNullException(nameof(scriptPubKey));
			

			var walletTransactions = _Cache.GetEntries();
			List<TransactionInformation> results = Filter(walletTransactions, !withProof, scriptPubKey);

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
			return results;
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

		private List<TransactionInformation> Filter(ICollection<RPCWalletEntry> entries, bool includeUnconf, Script scriptPubKey)
		{
			List<TransactionInformation> results = new List<TransactionInformation>();
			HashSet<uint256> resultsSet = new HashSet<uint256>();
			foreach(var obj in entries)
			{
				//May have duplicates
				if(!resultsSet.Contains(obj.TransactionId))
				{
					var confirmations = obj.Confirmations;
					var tx = _Cache.GetTransaction(obj.TransactionId);

					if(tx == null || (!includeUnconf && confirmations == 0))
						continue;

					if(tx.Outputs.Any(o => o.ScriptPubKey == scriptPubKey) ||
					   tx.Inputs.Any(o => o.ScriptSig.GetSigner().ScriptPubKey == scriptPubKey))
					{

						resultsSet.Add(obj.TransactionId);
						results.Add(new TransactionInformation()
						{
							Transaction = tx,
							Confirmations = confirmations
						});
					}
				}
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

		public async Task TrackAsync(Script scriptPubkey)
		{
			await _RPCBatch.Do(async batch =>
			{
				await batch.ImportAddressAsync(scriptPubkey, "", false).ConfigureAwait(false);
				return true;
			}).ConfigureAwait(false);
		}

		public int GetBlockConfirmations(uint256 blockId)
		{
			var result = RPCClient.SendCommandNoThrows("getblock", blockId.ToString(), true);
			if(result == null || result.Error != null)
				return 0;
			return (int)result.Result["confirmations"];
		}

		public async Task<bool> TrackPrunedTransactionAsync(Transaction transaction, MerkleBlock merkleProof)
		{
			return await _RPCBatch.Do(async batch =>
			{
				var result = await batch.SendCommandNoThrowsAsync("importprunedfunds", transaction.ToHex(), Encoders.Hex.EncodeData(merkleProof.ToBytes())).ConfigureAwait(false);
				var success = result != null && result.Error == null;
				if(success)
				{
					_Cache.ImportTransaction(transaction, GetBlockConfirmations(merkleProof.Header.GetHash()));
				}
				return success;
			}).ConfigureAwait(false);
		}
	}
}
