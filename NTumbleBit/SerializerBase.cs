using NTumbleBit.BouncyCastle.Math;
using NTumbleBit.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using NBitcoin;

namespace NTumbleBit
{
	internal class SerializerBase
	{
		public SerializerBase(Stream inner)
		{
			if(inner == null)
				throw new ArgumentNullException(nameof(inner));
			_Inner = inner;
		}
		
		protected bool littleEndian = true;

		public void WriteBytes(byte[] bytes, bool fixSize)
		{
			if(fixSize)
				Inner.Write(bytes, 0, bytes.Length);
			else
			{
				WriteUInt(bytes.Length);
				Inner.Write(bytes, 0, bytes.Length);
			}
		}

		internal const int KeySize = 256;
		public void WritePuzzleSolution(PuzzleSolution index)
		{
			WriteBigInteger(index._Value, KeySize);
		}		

		public PuzzleSolution ReadPuzzleSolution()
		{
			return new PuzzleSolution(ReadBigInteger(KeySize));
		}


		public void WriteUInt(long length)
		{
			var size = NBitcoin.Utils.ToBytes((uint)length, littleEndian);
			Inner.Write(size, 0, size.Length);
		}

		public void WritePuzzle(PuzzleValue puzzle)
		{
			WriteBigInteger(puzzle._Value, KeySize);
		}
		
		public void WritePuzzleSolutionKey(SolutionKey key)
		{
			WriteBytes(key.ToBytes(true), true);
		}
		
		public void WriteQuotient(Quotient q)
		{
			WriteBigInteger(q._Value, KeySize);
		}

		public Quotient ReadQuotient()
		{
			return new Quotient(ReadBigInteger(KeySize));
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
		public PuzzleValue ReadPuzzle()
		{
			return new PuzzleValue(ReadBigInteger(KeySize));
		}

		public ulong ReadULong()
		{
			var bytes = ReadBytes(8);
			return ToUInt64(bytes, littleEndian);
		}

		public void WriteULong(ulong v)
		{
			var bytes = NBitcoin.Utils.ToBytes(v, littleEndian);
			WriteBytes(bytes, true);
		}

		internal static ulong ToUInt64(byte[] value, bool littleEndian)
		{
			if(littleEndian)
			{
				return value[0]
					   + ((ulong)value[1] << 8)
					   + ((ulong)value[2] << 16)
					   + ((ulong)value[3] << 24)
					   + ((ulong)value[4] << 32)
					   + ((ulong)value[5] << 40)
					   + ((ulong)value[6] << 48)
					   + ((ulong)value[7] << 56);
			}
			else
			{
				return value[7]
					+ ((ulong)value[6] << 8)
					+ ((ulong)value[5] << 16)
					+ ((ulong)value[4] << 24)
					+ ((ulong)value[3] << 32)
					   + ((ulong)value[2] << 40)
					   + ((ulong)value[1] << 48)
					   + ((ulong)value[0] << 56);
			}
		}

		public byte[] ReadBytes(long size = -1, long maxSize = 1024)
		{
			if(size == -1)
			{
				size = ReadUInt();
			}

			if(size > maxSize || size < 0)
				throw new FormatException("Byte array too big to deserialize");
			byte[] result = new byte[size];
			Inner.Read(result, 0, result.Length);
			return result;
		}

		public long ReadUInt()
		{
			long size;
			var sizeBytes = new byte[4];
			Inner.Read(sizeBytes, 0, sizeBytes.Length);
			size = NBitcoin.Utils.ToUInt32(sizeBytes, littleEndian);
			return size;
		}

		internal BigInteger ReadBigInteger(int size)
		{
			var result = new byte[size];
			Inner.Read(result, 0, size);
			if(littleEndian)
				Array.Reverse(result);
			return new BigInteger(1, result);
		}

		internal void WriteBigInteger(BigInteger value, int size)
		{
			var bytes = value.ToByteArrayUnsigned();
			Utils.Pad(ref bytes, size);
			if(littleEndian)
				Array.Reverse(bytes);
			WriteBytes(bytes, true);
		}


		private readonly Stream _Inner;
		public Stream Inner
		{
			get
			{
				return _Inner;
			}
		}
	}
}
