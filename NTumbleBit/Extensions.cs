using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public static class Extensions
	{
		public static void ReadWriteC(this BitcoinStream bs, ref uint256[] values)
		{
			var mutable = values?.Select(h => h.AsBitcoinSerializable()).ToArray();
			bs.ReadWrite(ref mutable);
			if(!bs.Serializing)
			{
				values = mutable.Select(m => m.Value).ToArray();
			}
		}
		public static void ReadWriteC(this BitcoinStream bs, ref PubKey pubKey)
		{
			if(bs.Serializing)
			{
				var bytes = pubKey.ToBytes();
				bs.Inner.Write(bytes, 0, 33);
			}
			else
			{
				pubKey = new PubKey(bs.Inner.ReadBytes(33));
			}
		}

		public static void ReadWriteC(this BitcoinStream bs, ref Network network)
		{
			if(bs.Serializing)
			{
				var str = network.ToString();
				bs.ReadWriteC(ref str);
			}
			else
			{
				var str = string.Empty;
				bs.ReadWriteC(ref str);
				network = Network.GetNetwork(str);
			}
		}

		internal static void ReadWriteC(this BitcoinStream bs, ref BouncyCastle.Math.BigInteger integer)
		{
			if(bs.Serializing)
			{
				var str = integer.ToByteArrayUnsigned();
				bs.ReadWriteAsVarString(ref str);
			}
			else
			{
				byte[] str = null;
				bs.ReadWriteAsVarString(ref str);
				integer = new BouncyCastle.Math.BigInteger(1, str);
			}
		}

		public static void ReadWriteC(this BitcoinStream bs, ref string str)
		{
			if(bs.Serializing)
			{
				var bytes = Encoding.ASCII.GetBytes(str);
				bs.ReadWriteAsVarString(ref bytes);
			}
			else
			{
				byte[] bytes = null;
				bs.ReadWriteAsVarString(ref bytes);
				str = Encoding.ASCII.GetString(bytes);
			}
		}

		public static void ReadWriteC(this BitcoinStream bs, ref RsaPubKey pubKey)
		{
			if(bs.Serializing)
			{
				var bytes = pubKey == null ? new byte[0] : pubKey.ToBytes();
				bs.ReadWriteAsVarString(ref bytes);
			}
			else
			{
				byte[] bytes = null;
				bs.ReadWriteAsVarString(ref bytes);
				pubKey = bytes.Length == 0 ? null : new RsaPubKey(bytes);
			}
		}

		public static void ReadWriteC(this BitcoinStream bs, ref Money money)
		{
			if(bs.Serializing)
			{
				var satoshis = checked((ulong)money.Satoshi);
				bs.ReadWrite(ref satoshis);
			}
			else
			{
				var satoshis = 0UL;
				bs.ReadWrite(ref satoshis);
				money = Money.Satoshis(satoshis);
			}
		}

		public static RPCResponse SendCommandNoThrows(this RPCClient client, string commandName, params object[] parameters)
		{
			return client.SendCommand(new RPCRequest
			{
				Method = commandName,
				Params = parameters
			}, throwIfRPCError: false);
		}

		public static ScriptCoin Clone(this ScriptCoin scriptCoin)
		{
			return new ScriptCoin(scriptCoin, scriptCoin.Redeem);
		}
	}
}
