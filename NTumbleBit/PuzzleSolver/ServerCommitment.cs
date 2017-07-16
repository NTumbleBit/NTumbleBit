using NBitcoin;
using NBitcoin.Crypto;
using Newtonsoft.Json;
using NTumbleBit.JsonConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
	public class ServerCommitment : IBitcoinSerializable
	{
		public ServerCommitment()
		{

		}
		public ServerCommitment(uint160 keyHash, byte[] encryptedSolution)
		{
			EncryptedSolution = encryptedSolution;
			KeyHash = keyHash;
		}		

		byte[] _EncryptedSolution;
		public byte[] EncryptedSolution
		{
			get
			{
				return _EncryptedSolution;
			}
			set
			{
				_EncryptedSolution = value;
			}
		}

		uint160 _KeyHash;
		public uint160 KeyHash
		{
			get
			{
				return _KeyHash;
			}
			set
			{
				_KeyHash = value;
			}
		}

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWriteAsVarString(ref _EncryptedSolution);
			stream.ReadWrite(ref _KeyHash);
		}
	}
}
