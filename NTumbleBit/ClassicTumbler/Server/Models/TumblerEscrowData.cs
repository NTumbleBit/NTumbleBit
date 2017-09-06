using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace NTumbleBit.ClassicTumbler.Server.Models
{
	public class TumblerEscrowData : IBitcoinSerializable
	{
		public TumblerEscrowData()
		{

		}

		uint _OutputIndex;
		public int OutputIndex
		{
			get
			{
				return checked((int)_OutputIndex);
			}
			set
			{
				_OutputIndex = checked((uint)value);
			}
		}



		PubKey _EscrowInitiatorKey;
		public PubKey EscrowInitiatorKey
		{
			get
			{
				return _EscrowInitiatorKey;
			}
			set
			{
				_EscrowInitiatorKey = value;
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

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWriteAsVarInt(ref _OutputIndex);
			stream.ReadWrite(ref _Transaction);
			stream.ReadWrite(ref _MerkleProof);
			stream.ReadWriteC(ref _EscrowInitiatorKey);
		}
	}
}
