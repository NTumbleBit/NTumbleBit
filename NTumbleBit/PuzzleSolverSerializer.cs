using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NTumbleBit.BouncyCastle.Math;
using NBitcoin.Protocol;
using NBitcoin;

namespace NTumbleBit
{
	public class PuzzleSolverSerializer
	{
		public PuzzleSolverSerializer(PuzzleSolverParameters parameters, Stream inner)
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


		private readonly PuzzleSolverParameters _Parameters;
		public PuzzleSolverParameters Parameters
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

		public void WritePuzzleCommitments(PuzzleCommitment[] commitments)
		{
			if(commitments.Length != TotalCount)
				throw new ArgumentException("Commitment count invalid");
			foreach(var commitment in commitments)
			{
				WriteBytes(commitment.EncryptedSolution, false);
				WriteBytes(commitment.KeyHash.ToBytes(littleEndian), true);
			}
		}

		public PuzzleCommitment[] ReadPuzzleCommitments()
		{
			var commitments = new PuzzleCommitment[TotalCount];
			for(int i = 0; i < TotalCount; i++)
			{
				var encrypted = ReadBytes();
				var keyHash = new uint160(ReadBytes(20), littleEndian);
				commitments[i] = new PuzzleCommitment(keyHash, encrypted);
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
			Pad(ref bytes, size);
			WriteBytes(bytes, true);
		}

		private void Pad(ref byte[] bytes, int keySize)
		{
			int paddSize = keySize - bytes.Length;
			if(bytes.Length == keySize)
				return;
			if(paddSize < 0)
				throw new InvalidOperationException("Bug in NTumbleBit, copy the stacktrace and send us");
			var padded = new byte[paddSize + bytes.Length];
			Array.Copy(bytes, 0, padded, paddSize, bytes.Length);
			bytes = padded;
		}

		public void WritePuzzleRevelation(FakePuzzlesRevelation revelation)
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

		public FakePuzzlesRevelation ReadPuzzleRevelation()
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
			return new FakePuzzlesRevelation(indexes, solutions);
		}

		public void WritePuzzleSolutionKeys(PuzzleSolutionKey[] keys, bool real)
		{
			if(keys.Length != PuzzleSolutionKeysLength(real))
				throw new ArgumentException("keys count incorrect");
			foreach(var key in keys)
			{
				WriteBytes(key.ToBytes(true), true);
			}
		}

		public PuzzleSolutionKey[] ReadPuzzleSolutionKeys(bool real)
		{
			PuzzleSolutionKey[] keys = new PuzzleSolutionKey[PuzzleSolutionKeysLength(real)];
			for(int i = 0; i < keys.Length; i++)
			{
				keys[i] = new PuzzleSolutionKey(ReadBytes(Utils.ChachaKeySize));
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
