using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler.Server.Models
{
	public class TumblerEscrowKeyResponse : IBitcoinSerializable
	{

		int _KeyIndex;
		public int KeyIndex
		{
			get
			{
				return _KeyIndex;
			}
			set
			{
				_KeyIndex = value;
			}
		}


		PubKey _PubKey;
		public PubKey PubKey
		{
			get
			{
				return _PubKey;
			}
			set
			{
				_PubKey = value;
			}
		}


		uint160 _ChannelId;
		public uint160 ChannelId
		{
			get
			{
				return _ChannelId;
			}
			set
			{
				_ChannelId = value;
			}
		}

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWrite(ref _KeyIndex);
			stream.ReadWriteC(ref _PubKey);
			stream.ReadWrite(ref _ChannelId);
		}
	}
}
