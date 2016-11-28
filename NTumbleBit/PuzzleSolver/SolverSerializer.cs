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
	public class SolverSerializer
	{
		public SolverSerializer(SolverParameters parameters, Stream inner)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			if(inner == null)
				throw new ArgumentNullException("inner");
			_Parameters = parameters;
			_Inner = inner;
		}

		public int GetKeySize()
		{
			var keySize = _Parameters.ServerKey._Key.Modulus.ToByteArrayUnsigned().Length;
			while(keySize % 32 != 0)
				keySize++;
			return keySize;
		}


		private readonly SolverParameters _Parameters;
		public SolverParameters Parameters
		{
			get
			{
				return _Parameters;
			}
		}

		private readonly Stream _Inner;
		public Stream Inner
		{
			get
			{
				return _Inner;
			}
		}

		int TotalCount
		{
			get
			{
				return Parameters.FakePuzzleCount + Parameters.RealPuzzleCount;
			}
		}

		public void WritePuzzles(PuzzleValue[] puzzles)
		{
			if(puzzles == null)
				throw new ArgumentNullException("puzzles");
			if(puzzles.Length != TotalCount)
				throw new ArgumentException("puzzle count incorrect");
			foreach(var puzzle in puzzles)
			{
				WriteBigInteger(puzzle._Value, GetKeySize());
			}
		}

		public PuzzleValue[] ReadPuzzles()
		{
			PuzzleValue[] result = new PuzzleValue[TotalCount];
			for(int i = 0; i < result.Length; i++)
			{
				result[i] = new PuzzleValue(ReadBigInteger(GetKeySize()));
			}
			return result;
		}

		public void WritePuzzleCommitments(ServerCommitment[] commitments)
		{
			if(commitments.Length != TotalCount)
				throw new ArgumentException("Commitment count invalid");
			foreach(var commitment in commitments)
			{
				WriteBytes(commitment.EncryptedSolution, false);
				WriteBytes(commitment.KeyHash.ToBytes(littleEndian), true);
			}
		}

		public ServerCommitment[] ReadPuzzleCommitments()
		{
			var commitments = new ServerCommitment[TotalCount];
			for(int i = 0; i < TotalCount; i++)
			{
				var encrypted = ReadBytes();
				var keyHash = new uint160(ReadBytes(20), littleEndian);
				commitments[i] = new ServerCommitment(keyHash, encrypted);
			}
			return commitments;
		}

		bool littleEndian = true;

		private void WriteBytes(byte[] bytes, bool fixSize)
		{
			if(fixSize)
				Inner.Write(bytes, 0, bytes.Length);
			else
			{
				WriteUInt(bytes.Length);
				Inner.Write(bytes, 0, bytes.Length);
			}
		}

		private void WriteUInt(long length)
		{
			var size = NBitcoin.Utils.ToBytes((uint)length, littleEndian);
			Inner.Write(size, 0, size.Length);
		}

		public byte[] ReadBytes(long size = -1)
		{
			if(size == -1)
			{
				size = ReadUInt();
			}

			if(size > 1024 || size < 0)
				throw new FormatException("Byte array too big to deserialize");
			byte[] result = new byte[size];
			Inner.Read(result, 0, result.Length);
			return result;
		}

		private long ReadUInt()
		{
			long size;
			var sizeBytes = new byte[4];
			Inner.Read(sizeBytes, 0, sizeBytes.Length);
			size = NBitcoin.Utils.ToUInt32(sizeBytes, littleEndian);
			return size;
		}

		private BigInteger ReadBigInteger(int size)
		{
			var result = new byte[size];
			Inner.Read(result, 0, size);
			return new BigInteger(1, result);
		}

		private void WriteBigInteger(BigInteger value, int size)
		{
			var bytes = value.ToByteArrayUnsigned();
			Utils.Pad(ref bytes, size);
			WriteBytes(bytes, true);
		}
		

		public void WritePuzzleRevelation(ClientRevelation revelation)
		{
			if(revelation.Indexes.Length != Parameters.FakePuzzleCount || revelation.Solutions.Length != Parameters.FakePuzzleCount)
				throw new ArgumentException("Revelation invalid");
			
			foreach(var index in revelation.Indexes)
			{
				WriteUInt(index);
			}
			foreach(var index in revelation.Solutions)
			{
				WriteBigInteger(index._Value, GetKeySize());
			}
		}

		public ClientRevelation ReadPuzzleRevelation()
		{
			int[] indexes = new int[Parameters.FakePuzzleCount];
			for(int i = 0; i < Parameters.FakePuzzleCount; i++)
			{
				indexes[i] = (int)ReadUInt();
			}
			PuzzleSolution[] solutions = new PuzzleSolution[Parameters.FakePuzzleCount];
			for(int i = 0; i < Parameters.FakePuzzleCount; i++)
			{
				solutions[i] = new PuzzleSolution(ReadBigInteger(GetKeySize()));
			}
			return new ClientRevelation(indexes, solutions);
		}

		public void WritePuzzleSolutionKeys(SolutionKey[] keys, bool real)
		{
			if(keys.Length != PuzzleSolutionKeysLength(real))
				throw new ArgumentException("keys count incorrect");
			foreach(var key in keys)
			{
				WriteBytes(key.ToBytes(true), true);
			}
		}

		public SolutionKey[] ReadPuzzleSolutionKeys(bool real)
		{
			SolutionKey[] keys = new SolutionKey[PuzzleSolutionKeysLength(real)];
			for(int i = 0; i < keys.Length; i++)
			{
				keys[i] = new SolutionKey(ReadBytes(Utils.ChachaKeySize));
			}
			return keys;
		}

		private int PuzzleSolutionKeysLength(bool real)
		{
			return real ? Parameters.RealPuzzleCount : Parameters.FakePuzzleCount;
		}

		public void WriteBlindFactors(BlindFactor[] blindFactors)
		{
			if(blindFactors.Length != Parameters.RealPuzzleCount)
				throw new ArgumentException("Blind factor count incorrect");
			foreach(var b in blindFactors)
			{
				WriteBigInteger(b._Value, GetKeySize());
			}
		}

		public BlindFactor[] ReadBlindFactors()
		{
			BlindFactor[] blinds = new BlindFactor[Parameters.RealPuzzleCount];
			for(int i = 0; i < blinds.Length; i++)
			{
				blinds[i] = new BlindFactor(ReadBigInteger(GetKeySize()));
			}
			return blinds;
		}
	}
}
