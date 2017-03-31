using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using NTumbleBit.PuzzlePromise;
using NBitcoin.DataEncoders;

#if !CLIENT
namespace NTumbleBit.TumblerServer.Services.RPCServices
#else
namespace NTumbleBit.Client.Tumbler.Services.RPCServices
#endif
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

		public IDestination GenerateAddress(string label)
		{
			var result = _RPCClient.SendCommand("getnewaddress", label ?? "");
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

			var changeAddress = BitcoinAddress.Create(_RPCClient.SendCommand("getrawchangeaddress").ResultString, _RPCClient.Network);


			var result = _RPCClient.SendCommandNoThrows("fundrawtransaction", ToHex(tx), new JObject
			{
				new JProperty("lockUnspents", true),
				new JProperty("feeRate", feeRate.GetFee(1000).ToDecimal(MoneyUnit.BTC)),
				new JProperty("changeAddress", changeAddress.ToString()),
			});
			if(result.Error != null)
			{
				if(result.Error.Message.Equals("Insufficient funds", StringComparison.OrdinalIgnoreCase))
					return null;
				result.ThrowIfError();
			}
			var jobj = (JObject)result.Result;
			var hex = jobj["hex"].Value<string>();
			tx = new Transaction(hex);
			result = _RPCClient.SendCommand("signrawtransaction", tx.ToHex());
			jobj = (JObject)result.Result;
			hex = jobj["hex"].Value<string>();
			return new Transaction(hex);
		}

		//NBitcoin internally put a bit in the version number to make difference between transaction without input and transaction with witness.
		private string ToHex(Transaction tx)
		{
			// if there is inputs, then it can't be confusing
			if(tx.Inputs.Count > 0)
				return tx.ToHex();
			// if there is, do this ACK so that NBitcoin does not change the version number
			return Encoders.Hex.EncodeData(tx.ToBytes(NBitcoin.Protocol.ProtocolVersion.WITNESS_VERSION - 1));
		}
	}
}
