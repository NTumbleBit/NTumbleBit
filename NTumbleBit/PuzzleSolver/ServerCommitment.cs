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
	public class ServerCommitment
	{
		public ServerCommitment(uint160 keyHash, byte[] encryptedSolution)
		{
			EncryptedSolution = encryptedSolution;
			KeyHash = keyHash;
		}

		public byte[] EncryptedSolution
		{
			get; set;
		}

		public uint160 KeyHash
		{
			get; set;
		}
	}
}
