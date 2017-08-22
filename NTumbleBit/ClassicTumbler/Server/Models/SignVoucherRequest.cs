using NBitcoin;
using NTumbleBit.ClassicTumbler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler.Server.Models
{
	public class SignVoucherRequest : IBitcoinSerializable
	{
		int _Cycle;
		public int Cycle
		{
			get
			{
				return _Cycle;
			}
			set
			{
				_Cycle = value;
			}
		}


		int _KeyReference;
		public int KeyReference
		{
			get
			{
				return _KeyReference;
			}
			set
			{
				_KeyReference = value;
			}
		}


		PuzzleValue _UnsignedVoucher;
		public PuzzleValue UnsignedVoucher
		{
			get
			{
				return _UnsignedVoucher;
			}
			set
			{
				_UnsignedVoucher = value;
			}
		}


		MerkleBlock _MerkleProof;
		public MerkleBlock MerkleProof
		{
			get
			{
				return _MerkleProof;
			}
			set
			{
				_MerkleProof = value;
			}
		}


		PubKey _ClientEscrowKey;
		public PubKey ClientEscrowKey
		{
			get
			{
				return _ClientEscrowKey;
			}
			set
			{
				_ClientEscrowKey = value;
			}
		}


		Transaction _Transaction;
		public Transaction Transaction
		{
			get
			{
				return _Transaction;
			}
			set
			{
				_Transaction = value;
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
			stream.ReadWrite(ref _Cycle);
			stream.ReadWrite(ref _KeyReference);
			stream.ReadWrite(ref _UnsignedVoucher);
			stream.ReadWrite(ref _MerkleProof);
			stream.ReadWriteC(ref _ClientEscrowKey);
			stream.ReadWrite(ref _Transaction);
			stream.ReadWrite(ref _ChannelId);
		}
	}
}
