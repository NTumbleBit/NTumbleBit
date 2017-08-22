using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace NTumbleBit.ClassicTumbler.Server.Models
{
	public class OpenChannelResponse : IBitcoinSerializable
	{
		public OpenChannelResponse()
		{

		}
		public OpenChannelResponse(ScriptCoin coin)
		{
			ScriptCoin = coin;
		}
		public ScriptCoin ScriptCoin
		{
			get; set;
		}
		public uint160 ChannelId
		{
			get; set;
		}
		public void ReadWrite(BitcoinStream stream)
		{
			if(stream.Serializing)
			{
				stream.ReadWrite(ScriptCoin.Redeem);
				stream.ReadWrite(ScriptCoin.Outpoint);
				stream.ReadWrite(ScriptCoin.TxOut);
				stream.ReadWrite(ChannelId);
			}
			else
			{
				Script redeem = null;
				OutPoint outpoint = null;
				TxOut txout = null;
				uint160 channelId = null;
				stream.ReadWrite(ref redeem);
				stream.ReadWrite(ref outpoint);
				stream.ReadWrite(ref txout);
				stream.ReadWrite(ref channelId);
				ChannelId = channelId;
				ScriptCoin = new ScriptCoin(outpoint, txout, redeem);
			}
		}
	}
}
