using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using NTumbleBit.PuzzlePromise;
using NBitcoin.DataEncoders;

namespace NTumbleBit.Services.RPC
{
	public class RPCWalletService : IWalletService
	{
		public RPCWalletService(RPCClient rpc)
		{
			if(rpc == null)
				throw new ArgumentNullException(nameof(rpc));
			_RPCClient = rpc;
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

		public Transaction FundTransaction(TxOut txOut, FeeRate feeRate)
		{
			Transaction tx = new Transaction();
			tx.Outputs.Add(txOut);

			var changeAddress = _RPCClient.GetRawChangeAddress();

			FundRawTransactionResponse response = null;
			try
			{
				response = _RPCClient.FundRawTransaction(tx, new FundRawTransactionOptions()
				{
					ChangeAddress = changeAddress,
					FeeRate = feeRate,
					LockUnspents = true
				});
			}
			catch(RPCException ex)
			{
				var balance = _RPCClient.GetBalance(0, false);
				var needed = tx.Outputs.Select(o => o.Value).Sum()
							  + feeRate.GetFee(2000);
				var missing = needed - balance;
				if(missing > Money.Zero || ex.Message.Equals("Insufficient funds", StringComparison.OrdinalIgnoreCase))
					throw new NotEnoughFundsException("Not enough funds", "", missing);
				throw;
			}
			var result = _RPCClient.SendCommand("signrawtransaction", response.Transaction.ToHex());
			return new Transaction(((JObject)result.Result)["hex"].Value<string>());
		}
	}
}
