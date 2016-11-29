using NTumbleBit.BouncyCastle.Math;
using NTumbleBit.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class SerializerBase
	{
		internal SerializerBase(Stream inner, RsaKeyParameters parameters)
		{
			if(inner == null)
				throw new ArgumentNullException("inner");
			if(parameters != null)
			{
				var keySize = parameters.Modulus.ToByteArrayUnsigned().Length;
				while(keySize % 32 != 0)
					keySize++;
				_KeySize = keySize;
			}
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

		public void WriteUInt(long length)
		{
			var size = NBitcoin.Utils.ToBytes((uint)length, littleEndian);
			Inner.Write(size, 0, size.Length);
		}

		public void WritePuzzle(PuzzleValue puzzle)
		{
			WriteBigInteger(puzzle._Value, GetKeySize());
		}


		public PuzzleValue ReadPuzzle()
		{
			return new PuzzleValue(ReadBigInteger(GetKeySize()));
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
			return new BigInteger(1, result);
		}

		internal void WriteBigInteger(BigInteger value, int size)
		{
			var bytes = value.ToByteArrayUnsigned();
			Utils.Pad(ref bytes, size);
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

		int _KeySize;
		public int GetKeySize()
		{
			return _KeySize;
		}
	}
}
