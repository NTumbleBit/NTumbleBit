using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace NTumbleBit.ClassicTumbler.Server.Models
{
	public class ScriptCoinModel : IBitcoinSerializable
	{
		public ScriptCoinModel()
		{

		}
		public ScriptCoinModel(ScriptCoin coin)
		{
			ScriptCoin = coin;
		}
		public ScriptCoin ScriptCoin
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
			}
			else
			{
				Script redeem = null;
				OutPoint outpoint = null;
				TxOut txout = null;
				stream.ReadWrite(ref redeem);
				stream.ReadWrite(ref outpoint);
				stream.ReadWrite(ref txout);
				ScriptCoin = new ScriptCoin(outpoint, txout, redeem);
			}
		}
	}
}
