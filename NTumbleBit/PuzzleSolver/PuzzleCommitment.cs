using NBitcoin;
using NBitcoin.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
	public class PuzzleCommitment
	{
		public PuzzleCommitment(uint160 keyHash, byte[] encryptedSolution)
		{
			this.EncryptedSolution = encryptedSolution;
			this.KeyHash = keyHash;
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
