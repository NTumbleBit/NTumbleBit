using NBitcoin;
using Microsoft.Extensions.Logging;
using System.Linq;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NTumbleBit.Logging;

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
			_ReceiveBatch = new ReceiveBatch(rpc);
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
				_ReceiveBatch.BatchInterval = value;
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

		public async Task<IDestination> GenerateAddressAsync()
		{
			var address = await _RPCClient.GetNewAddressAsync().ConfigureAwait(false);
			var witAddress = await _RPCClient.SendCommandAsync("addwitnessaddress", address.ToString()).ConfigureAwait(false);
			return BitcoinAddress.Create(witAddress.ResultString, _RPCClient.Network);
		}

		public Coin AsCoin(UnspentCoin c)
		{
			var coin = new Coin(c.OutPoint, new TxOut(c.Amount, c.ScriptPubKey));
			if(c.RedeemScript != null)
				coin = coin.ToScriptCoin(c.RedeemScript);
			return coin;
		}

		

		FundingBatch _FundingBatch;
		public async Task<Transaction> FundTransactionAsync(TxOut txOut, FeeRate feeRate)
		{
			_FundingBatch.FeeRate = feeRate;
			var task = _FundingBatch.WaitTransactionAsync(txOut).ConfigureAwait(false);
			Logs.Tumbler.LogDebug($"TumblerEscrow batch count {_FundingBatch.BatchCount}");
			return await task;
		}

		

		ReceiveBatch _ReceiveBatch;
		public async Task<Transaction> ReceiveAsync(ScriptCoin escrowedCoin, TransactionSignature clientSignature, Key escrowKey, FeeRate feeRate)
		{
			_ReceiveBatch.FeeRate = feeRate;
			var task = _ReceiveBatch.WaitTransactionAsync(new ClientEscapeData()
			{
				ClientSignature = clientSignature,
				EscrowedCoin = escrowedCoin,
				EscrowKey = escrowKey
			}).ConfigureAwait(false);
			Logs.Tumbler.LogDebug($"ClientEscape batch count {_ReceiveBatch.BatchCount}");
			return await task;
		}
	}
}
