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
	class FundingBatch : BatchBase<TxOut, Transaction>
	{
		public FundingBatch(RPCClient rpc)
		{
			_RPCClient = rpc;
		}
		RPCClient _RPCClient;
		public FeeRate FeeRate
		{
			get; set;
		}

		protected override async Task<Transaction> RunAsync(TxOut[] data)
		{
			Utils.Shuffle(data);
			var tx = new Transaction();
			tx.Outputs.AddRange(data);
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
				var balance = await _RPCClient.GetBalanceAsync(0, false).ConfigureAwait(false);
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
}
