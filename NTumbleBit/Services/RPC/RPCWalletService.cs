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

		

		FundingBatch _FundingBatch;
		public async Task<Transaction> FundTransactionAsync(TxOut txOut, FeeRate feeRate)
		{
			_FundingBatch.FeeRate = feeRate;
			return await _FundingBatch.WaitTransactionAsync(txOut).ConfigureAwait(false);
		}

		

		ReceiveBatch _ReceiveBatch;
		public async Task<Transaction> ReceiveAsync(ScriptCoin escrowedCoin, TransactionSignature clientSignature, Key escrowKey, FeeRate feeRate)
		{
			_ReceiveBatch.FeeRate = feeRate;
			return await _ReceiveBatch.WaitTransactionAsync(new ClientEscapeData()
			{
				ClientSignature = clientSignature,
				EscrowedCoin = escrowedCoin,
				EscrowKey = escrowKey
			}).ConfigureAwait(false);
		}
	}
}
