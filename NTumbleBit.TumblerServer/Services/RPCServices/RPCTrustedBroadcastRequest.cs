using NBitcoin;
using NBitcoin.RPC;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#if !CLIENT
namespace NTumbleBit.TumblerServer.Services.RPCServices
#else
namespace NTumbleBit.Client.Tumbler.Services.RPCServices
#endif
{
	public class RPCTrustedBroadcastService : ITrustedBroadcastService
	{
		public RPCTrustedBroadcastService(RPCClient rpc, IBroadcastService innerBroadcast, IRepository repository)
		{
			if(rpc == null)
				throw new ArgumentNullException("rpc");
			if(innerBroadcast == null)
				throw new ArgumentNullException("innerBroadcast");
			if(repository == null)
				throw new ArgumentNullException("repository");
			_Repository = repository;
			_RPCClient = rpc;
			_Broadcaster = innerBroadcast;
		}

		IBroadcastService _Broadcaster;

		private readonly RPCClient _RPCClient;
		public RPCClient RPCClient
		{
			get
			{
				return _RPCClient;
			}
		}

		public void Broadcast(TrustedBroadcastRequest broadcast)
		{
			if(broadcast == null)
				throw new ArgumentNullException("broadcast");
			var address = broadcast.PreviousScriptPubKey.GetDestinationAddress(RPCClient.Network);
			if(address == null)
				throw new NotSupportedException("ScriptPubKey to track not supported");
			RPCClient.ImportAddress(address, "", false);
			
			AddBroadcast(broadcast);
			var height = RPCClient.GetBlockCount();
			if(height < broadcast.BroadcastAt.Height)
				return;
			_Broadcaster.Broadcast(broadcast.Transaction);
		}

		private void AddBroadcast(TrustedBroadcastRequest broadcast)
		{
			_Repository.Add("TrustedBroadcasts", broadcast.Transaction.GetHash().ToString(), broadcast);
		}

		private readonly IRepository _Repository;
		public IRepository Repository
		{
			get
			{
				return _Repository;
			}
		}

		public TrustedBroadcastRequest[] GetRequests()
		{
			return Repository.List<TrustedBroadcastRequest>("TrustedBroadcasts");
		}

		public Transaction[] TryBroadcast()
		{
			var height = RPCClient.GetBlockCount();
			List<Transaction> broadcasted = new List<Transaction>();
			foreach(var broadcast in GetRequests())
			{
				if(height < broadcast.BroadcastAt.Height)
					continue;

				foreach(var tx in GetReceivedTransactions(broadcast.PreviousScriptPubKey))
				{
					foreach(var coin in tx.Outputs.AsCoins())
					{
						if(coin.ScriptPubKey == broadcast.PreviousScriptPubKey)
						{
							var transaction = broadcast.ReSign(coin);
							if(_Broadcaster.Broadcast(transaction))
								broadcasted.Add(transaction);
						}
					}
				}
			}
			return broadcasted.ToArray();
		}


		public Transaction[] GetReceivedTransactions(Script scriptPubKey)
		{
			if(scriptPubKey == null)
				throw new ArgumentNullException("scriptPubKey");

			var address = scriptPubKey.GetDestinationAddress(RPCClient.Network);
			if(address == null)
				return new Transaction[0];


			var result = RPCClient.SendCommandNoThrows("listtransactions", "", 100, 0, true);
			if(result.Error != null)
				return null;

			var transactions = (Newtonsoft.Json.Linq.JArray)result.Result;
			List<TransactionInformation> results = new List<TransactionInformation>();
			foreach(var obj in transactions)
			{
				var txId = new uint256((string)obj["txid"]);
				if((string)obj["address"] == address.ToString())
				{
					var tx = GetTransaction(txId);
					if(tx != null)
						if((string)obj["category"] == "receive")
						{
							results.Add(tx);
						}
				}
			}
			return results.Select(t => t.Transaction).ToArray();
		}

		public TransactionInformation GetTransaction(uint256 txId)
		{
			var result = RPCClient.SendCommandNoThrows("getrawtransaction", txId.ToString(), 1);
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
	}


}
