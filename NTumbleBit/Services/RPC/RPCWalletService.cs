using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using NTumbleBit.PuzzlePromise;
using NBitcoin.DataEncoders;
using System.Collections.Concurrent;

namespace NTumbleBit.Services.RPC
{
	public class RPCWalletService : IWalletService
	{
		public RPCWalletService(RPCClient rpc)
		{
			if(rpc == null)
				throw new ArgumentNullException(nameof(rpc));
			_RPCClient = rpc;
			_FundingBatch = new FundingBatch(rpc);
			BatchInterval = TimeSpan.Zero;
		}

		public TimeSpan BatchInterval
		{
			get
			{
				return _FundingBatch.BatchInterval;
			}
			set
			{
				_FundingBatch.BatchInterval = value;
			}
		}

		private readonly RPCClient _RPCClient;
		public RPCClient RPCClient
		{
			get
			{
				return _RPCClient;
			}
		}

		public IDestination GenerateAddress()
		{
			var result = _RPCClient.SendCommand("getnewaddress", "");
			return BitcoinAddress.Create(result.ResultString, _RPCClient.Network);
		}

		public Coin AsCoin(UnspentCoin c)
		{
			var coin = new Coin(c.OutPoint, new TxOut(c.Amount, c.ScriptPubKey));
			if(c.RedeemScript != null)
				coin = coin.ToScriptCoin(c.RedeemScript);
			return coin;
		}

		class FundingBatch
		{
			public FundingBatch(RPCClient rpc)
			{
				_RPCClient = rpc;
			}

			public TimeSpan BatchInterval
			{
				get; set;
			}
			RPCClient _RPCClient;
			public FeeRate FeeRate
			{
				get; set;
			}

			ConcurrentQueue<TxOut> Outputs = new ConcurrentQueue<TxOut>();
			public async Task<Transaction> WaitTransactionAsync(TxOut output)
			{
				var isFirstOutput = false;
				TaskCompletionSource<Transaction> completion = null;
				lock(Outputs)
				{
					completion = _TransactionCreated;
					Outputs.Enqueue(output);
					isFirstOutput = Outputs.Count == 1;
				}
				if(isFirstOutput)
				{
					await Task.WhenAny(completion.Task, Task.Delay(BatchInterval)).ConfigureAwait(false);
					if(completion.Task.Status != TaskStatus.RanToCompletion &&
						completion.Task.Status != TaskStatus.Faulted)
						SendNow();
				}
				return await completion.Task.ConfigureAwait(false);
			}

			TaskCompletionSource<Transaction> _TransactionCreated = new TaskCompletionSource<Transaction>();

			public void SendNow()
			{
				var unused = FundTransactionAsync();
			}

			private async Task FundTransactionAsync()
			{
				Transaction tx = new Transaction();
				List<TxOut> outputs = new List<TxOut>();
				TxOut output = new TxOut();
				TaskCompletionSource<Transaction> completion = null;
				lock(Outputs)
				{
					completion = _TransactionCreated;
					_TransactionCreated = new TaskCompletionSource<Transaction>();
					while(Outputs.TryDequeue(out output))
					{
						outputs.Add(output);
					}
				}
				if(outputs.Count == 0)
					return;
				var outputsArray = outputs.ToArray();
				NBitcoin.Utils.Shuffle(outputsArray);
				tx.Outputs.AddRange(outputsArray);

				try
				{
					completion.TrySetResult(await FundTransactionAsync(tx).ConfigureAwait(false));
				}
				catch(Exception ex)
				{
					completion.TrySetException(ex);
				}
			}

			private async Task<Transaction> FundTransactionAsync(Transaction tx)
			{
				var changeAddress = _RPCClient.GetRawChangeAddress();

				FundRawTransactionResponse response = null;
				try
				{
					response = await _RPCClient.FundRawTransactionAsync(tx, new FundRawTransactionOptions()
					{
						ChangeAddress = changeAddress,
						FeeRate = FeeRate,
						LockUnspents = true
					}).ConfigureAwait(false);
				}
				catch(RPCException ex)
				{
					var balance = _RPCClient.GetBalance(0, false);
					var needed = tx.Outputs.Select(o => o.Value).Sum()
								  + FeeRate.GetFee(2000);
					var missing = needed - balance;
					if(missing > Money.Zero || ex.Message.Equals("Insufficient funds", StringComparison.OrdinalIgnoreCase))
						throw new NotEnoughFundsException("Not enough funds", "", missing);
					throw;
				}
				var result = await _RPCClient.SendCommandAsync("signrawtransaction", response.Transaction.ToHex()).ConfigureAwait(false);
				return new Transaction(((JObject)result.Result)["hex"].Value<string>());
			}
		}

		FundingBatch _FundingBatch;
		public async Task<Transaction> FundTransactionAsync(TxOut txOut, FeeRate feeRate)
		{
			_FundingBatch.FeeRate = feeRate;
			return await _FundingBatch.WaitTransactionAsync(txOut).ConfigureAwait(false);
		}
	}
}
