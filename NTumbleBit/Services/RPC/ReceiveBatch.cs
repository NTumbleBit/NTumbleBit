using NBitcoin;
using System.Linq;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.Services.RPC
{
	abstract class BatchBase<T, TResult>
	{
		public TimeSpan BatchInterval
		{
			get; set;
		}

		public int BatchCount
		{
			get
			{
				return Data.Count;
			}
		}
		protected ConcurrentQueue<T> Data = new ConcurrentQueue<T>();
		public async Task<TResult> WaitTransactionAsync(T data)
		{
			var isFirstOutput = false;
			TaskCompletionSource<TResult> completion = null;
			lock(Data)
			{
				completion = _TransactionCreated;
				Data.Enqueue(data);
				isFirstOutput = Data.Count == 1;
			}
			if(isFirstOutput)
			{
				await Task.WhenAny(completion.Task, Task.Delay(BatchInterval)).ConfigureAwait(false);
				if(completion.Task.Status != TaskStatus.RanToCompletion &&
					completion.Task.Status != TaskStatus.Faulted)
				{
					await MakeTransactionAsync().ConfigureAwait(false);
				}
			}
			return await completion.Task.ConfigureAwait(false);
		}

		protected async Task MakeTransactionAsync()
		{
			
			List<T> data = new List<T>();
			T output = default(T);
			TaskCompletionSource<TResult> completion = null;
			lock(Data)
			{
				completion = _TransactionCreated;
				_TransactionCreated = new TaskCompletionSource<TResult>();
				while(Data.TryDequeue(out output))
				{
					data.Add(output);
				}
			}
			if(data.Count == 0)
				return;
			var dataArray = data.ToArray();
			NBitcoin.Utils.Shuffle(dataArray);


			try
			{
				completion.TrySetResult(await RunAsync(dataArray).ConfigureAwait(false));
			}
			catch(Exception ex)
			{
				completion.TrySetException(ex);
			}
		}

		protected abstract Task<TResult> RunAsync(T[] data);

		TaskCompletionSource<TResult> _TransactionCreated = new TaskCompletionSource<TResult>();
	}
	class ClientEscapeData
	{
		public ScriptCoin EscrowedCoin
		{
			get;
			set;
		}
		public TransactionSignature ClientSignature
		{
			get;
			set;
		}
		public Key EscrowKey
		{
			get;
			set;
		}
	}
	class ReceiveBatch : BatchBase<ClientEscapeData, Transaction>
	{
		public ReceiveBatch(RPCClient rpc)
		{
			_RPCClient = rpc;
		}

	
		RPCClient _RPCClient;
		public FeeRate FeeRate
		{
			get; set;
		}
		
		protected override async Task<Transaction> RunAsync(ClientEscapeData[] data)
		{
			Utils.Shuffle(data);
			var cashout = await _RPCClient.GetNewAddressAsync().ConfigureAwait(false);
			var tx = new Transaction();
			foreach(var input in data)
			{
				var txin = new TxIn(input.EscrowedCoin.Outpoint);
				txin.ScriptSig = new Script(
				Op.GetPushOp(TrustedBroadcastRequest.PlaceholderSignature),
				Op.GetPushOp(TrustedBroadcastRequest.PlaceholderSignature),
				Op.GetPushOp(input.EscrowedCoin.Redeem.ToBytes())
				);
				txin.Witnessify();
				tx.AddInput(txin);
			}

			tx.Outputs.Add(new TxOut()
			{
				ScriptPubKey = cashout.ScriptPubKey,
				Value = data.Select(c => c.EscrowedCoin.Amount).Sum()
			});

			//should be zero, but for later improvement...
			var currentFee = tx.GetFee(data.Select(d => d.EscrowedCoin).ToArray());
			tx.Outputs[0].Value -= FeeRate.GetFee(tx) - currentFee;

			for(int i = 0; i < data.Length; i++)
			{
				var input = data[i];
				var txin = tx.Inputs[i];
				var signature = tx.SignInput(input.EscrowKey, input.EscrowedCoin);
				txin.ScriptSig = new Script(
				Op.GetPushOp(input.ClientSignature.ToBytes()),
				Op.GetPushOp(signature.ToBytes()),
				Op.GetPushOp(input.EscrowedCoin.Redeem.ToBytes())
				);
				txin.Witnessify();
			}
			await _RPCClient.SendRawTransactionAsync(tx).ConfigureAwait(false);
			return tx;
		}
	}

}
