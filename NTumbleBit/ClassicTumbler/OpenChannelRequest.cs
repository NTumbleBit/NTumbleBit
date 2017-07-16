using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
    public class OpenChannelRequest : IBitcoinSerializable
    {
		

		PubKey _EscrowKey;
		public PubKey EscrowKey
		{
			get
			{
				return _EscrowKey;
			}
			set
			{
				_EscrowKey = value;
			}
		}


		byte[] _Signature;
		public byte[] Signature
		{
			get
			{
				return _Signature;
			}
			set
			{
				_Signature = value;
			}
		}


		uint160 _Nonce;
		public uint160 Nonce
		{
			get
			{
				return _Nonce;
			}
			set
			{
				_Nonce = value;
			}
		}


		int _CycleStart;
		public int CycleStart
		{
			get
			{
				return _CycleStart;
			}
			set
			{
				_CycleStart = value;
			}
		}

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWriteC(ref _EscrowKey);
			stream.ReadWriteAsVarString(ref _Signature);
			stream.ReadWrite(ref _Nonce);
			stream.ReadWrite(ref _CycleStart);
		}
	}
}
