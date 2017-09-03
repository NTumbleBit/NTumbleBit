using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json.Linq;
using NBitcoin.DataEncoders;
using System.Threading;
using System.Collections.Concurrent;

namespace NTumbleBit.Services.RPC
{
	public class RPCBlockExplorerService : IBlockExplorerService
	{
		RPCWalletCache _Cache;
		RPCBatch<bool> _RPCBatch;
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
			_RPCBatch = new RPCBatch<bool>(client);
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

		public async Task<ICollection<TransactionInformation>> GetTransactionsAsync(Script scriptPubKey, bool withProof)
		{
			if(scriptPubKey == null)
				throw new ArgumentNullException(nameof(scriptPubKey));


			var results = _Cache
										.GetEntriesFromScript(scriptPubKey)
										.Select(entry => new TransactionInformation()
										{
											Confirmations = entry.Confirmations,
											Transaction = entry.Transaction
										}).ToList();

			if(withProof)
			{

				foreach(var tx in results.ToList())
				{
					var completion = new TaskCompletionSource<MerkleBlock>();
					bool isRequester = true;
					var txid = tx.Transaction.GetHash();
					_GettingProof.AddOrUpdate(txid, completion, (k, o) =>
					{
						isRequester = false;
						completion = o;
						return o;
					});
					if(isRequester)
					{
						try
						{
							MerkleBlock proof = null;
							var result = await RPCClient.SendCommandNoThrowsAsync("gettxoutproof", new JArray(tx.Transaction.GetHash().ToString())).ConfigureAwait(false);
							if(result == null || result.Error != null)
							{
								completion.TrySetResult(null);
								continue;
							}
							proof = new MerkleBlock();
							proof.ReadWrite(Encoders.Hex.DecodeData(result.ResultString));
							tx.MerkleProof = proof;
							completion.TrySetResult(proof);
						}
						catch(Exception ex) { completion.TrySetException(ex); }
						finally { _GettingProof.TryRemove(txid, out completion); }
					}

					var merkleBlock = await completion.Task.ConfigureAwait(false);
					if(merkleBlock == null)
						results.Remove(tx);
				}
			}
			return results;
		}

		ConcurrentDictionary<uint256, TaskCompletionSource<MerkleBlock>> _GettingProof = new ConcurrentDictionary<uint256, TaskCompletionSource<MerkleBlock>>();

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
			await _RPCBatch.WaitTransactionAsync(async batch =>
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
			bool success = false;
			await _RPCBatch.WaitTransactionAsync(async batch =>
			{
				var result = await batch.SendCommandNoThrowsAsync("importprunedfunds", transaction.ToHex(), Encoders.Hex.EncodeData(merkleProof.ToBytes())).ConfigureAwait(false);
				success = result != null && result.Error == null;
				if(success)
				{
					_Cache.ImportTransaction(transaction, GetBlockConfirmations(merkleProof.Header.GetHash()));
				}
				return success;
			}).ConfigureAwait(false);
			return success;
		}
	}
}
