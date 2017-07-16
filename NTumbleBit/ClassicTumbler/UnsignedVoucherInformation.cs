using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
	public class UnsignedVoucherInformation : IBitcoinSerializable
	{
		PuzzleValue _Puzzle;
		public PuzzleValue Puzzle
		{
			get
			{
				return _Puzzle;
			}
			set
			{
				_Puzzle = value;
			}
		}


		byte[] _EncryptedSignature;
		public byte[] EncryptedSignature
		{
			get
			{
				return _EncryptedSignature;
			}
			set
			{
				_EncryptedSignature = value;
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
			stream.ReadWrite(ref _Puzzle);
			stream.ReadWriteAsVarString(ref _EncryptedSignature);
			stream.ReadWrite(ref _Nonce);
			stream.ReadWrite(ref _CycleStart);
		}
	}
}
