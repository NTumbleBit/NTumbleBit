using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NTumbleBit.BouncyCastle.Math;
using NBitcoin.Protocol;
using NBitcoin;

namespace NTumbleBit.PuzzleSolver
{
	public class SolverSerializer : SerializerBase
	{
		public SolverSerializer(Stream inner) : base(inner)
		{
			if(inner == null)
				throw new ArgumentNullException("inner");
		}
		public void WriteCommitment(ServerCommitment commitment)
		{
			WriteBytes(commitment.EncryptedSolution, false);
			WriteBytes(commitment.KeyHash.ToBytes(littleEndian), true);
		}		

		public void WritePuzzleSolutionKey(SolutionKey key)
		{
			WriteBytes(key.ToBytes(true), true);
		}

		public ServerCommitment ReadCommitment()
		{
			var encrypted = ReadBytes();
			var keyHash = new uint160(ReadBytes(20), littleEndian);
			var commitment = new ServerCommitment(keyHash, encrypted);
			return commitment;
		}		

		public SolutionKey ReadPuzzleSolutionKey()
		{
			return new SolutionKey(ReadBytes(Utils.ChachaKeySize));
		}		

		public void WriteBlindFactor(BlindFactor b)
		{
			WriteBigInteger(b._Value, KeySize);
		}		

		public BlindFactor ReadBlindFactor()
		{
			return new BlindFactor(ReadBigInteger(KeySize));
		}

		public SolverParameters ReadParameters()
		{
			int fakePuzzleCount, realPuzzleCount;
			fakePuzzleCount = (int)ReadUInt();
			realPuzzleCount = (int)ReadUInt();

			var bytes = ReadBytes(-1, 10 * 1024);
			var key = new RsaPubKey(bytes);
			return new SolverParameters(key) { FakePuzzleCount = fakePuzzleCount, RealPuzzleCount = realPuzzleCount };
		}
	}
}
