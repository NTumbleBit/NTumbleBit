using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
	public class PromiseSerializer : SerializerBase
	{
		public PromiseSerializer(Stream inner) : base(inner)
		{
			if(inner == null)
				throw new ArgumentNullException("inner");
		}
		
		public void WriteQuotient(Quotient q)
		{
			WriteBigInteger(q._Value, KeySize);
		}
		
		public Quotient ReadQuotient()
		{
			return new Quotient(ReadBigInteger(KeySize));
		}		

		public uint256 ReadUInt256()
		{
			return new uint256(ReadBytes(32), littleEndian);
		}

		public void WriteUInt256(uint256 hash)
		{
			WriteBytes(hash.ToBytes(littleEndian), littleEndian);
		}

		public ScriptCoin ReadScriptCoin()
		{
			var txId = ReadUInt256();
			var index = ReadUInt();
			var money = Money.Satoshis(ReadULong());
			var scriptPubKey = new Script(ReadBytes());
			var redeem = new Script(ReadBytes());
			return new ScriptCoin(txId, (uint)index, money, scriptPubKey, redeem);
		}
		public void WriteScriptCoin(ScriptCoin coin)
		{
			WriteUInt256(coin.Outpoint.Hash);
			WriteUInt(coin.Outpoint.N);
			WriteULong((ulong)coin.Amount.Satoshi);
			WriteBytes(coin.ScriptPubKey.ToBytes(), false);
			WriteBytes(coin.Redeem.ToBytes(), false);
		}		
	}
}
