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
		public SolverSerializer(SolverParameters parameters, Stream inner) : base(inner, parameters?.ServerKey?._Key)
		{
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			if(inner == null)
				throw new ArgumentNullException("inner");
			_Parameters = parameters;
		}


		private readonly SolverParameters _Parameters;
		public SolverParameters Parameters
		{
			get
			{
				return _Parameters;
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
				WritePuzzle(puzzle);
			}
		}

		public void WritePuzzle(PuzzleValue puzzle)
		{
			WriteBigInteger(puzzle._Value, GetKeySize());
		}

		public PuzzleValue[] ReadPuzzles()
		{
			PuzzleValue[] result = new PuzzleValue[TotalCount];
			for(int i = 0; i < result.Length; i++)
			{
				result[i] = ReadPuzzle();
			}
			return result;
		}

		public PuzzleValue ReadPuzzle()
		{
			return new PuzzleValue(ReadBigInteger(GetKeySize()));
		}

		public void WritePuzzleCommitments(ServerCommitment[] commitments)
		{
			if(commitments.Length != TotalCount)
				throw new ArgumentException("Commitment count invalid");
			foreach(var commitment in commitments)
			{
				WriteCommitment(commitment);
			}
		}

		public void WriteCommitment(ServerCommitment commitment)
		{
			WriteBytes(commitment.EncryptedSolution, false);
			WriteBytes(commitment.KeyHash.ToBytes(littleEndian), true);
		}

		public ServerCommitment[] ReadPuzzleCommitments()
		{
			var commitments = new ServerCommitment[TotalCount];
			for(int i = 0; i < TotalCount; i++)
			{
				ServerCommitment commitment = ReadCommitment();
				commitments[i] = commitment;
			}
			return commitments;
		}

		public ServerCommitment ReadCommitment()
		{
			var encrypted = ReadBytes();
			var keyHash = new uint160(ReadBytes(20), littleEndian);
			var commitment = new ServerCommitment(keyHash, encrypted);
			return commitment;
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
				WriteSolution(index);
			}
		}

		public void WriteSolution(PuzzleSolution index)
		{
			WriteBigInteger(index._Value, GetKeySize());
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
				solutions[i] = ReadPuzzleSolution();
			}
			return new ClientRevelation(indexes, solutions);
		}

		public PuzzleSolution ReadPuzzleSolution()
		{
			return new PuzzleSolution(ReadBigInteger(GetKeySize()));
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
				WriteBlindFactor(b);
			}
		}

		public void WriteBlindFactor(BlindFactor b)
		{
			WriteBigInteger(b._Value, GetKeySize());
		}

		public BlindFactor[] ReadBlindFactors()
		{
			BlindFactor[] blinds = new BlindFactor[Parameters.RealPuzzleCount];
			for(int i = 0; i < blinds.Length; i++)
			{
				blinds[i] = ReadBlindFactor();
			}
			return blinds;
		}

		public BlindFactor ReadBlindFactor()
		{
			return new BlindFactor(ReadBigInteger(GetKeySize()));
		}

		public void WriteParameters()
		{
			WriteUInt(Parameters.FakePuzzleCount);
			WriteUInt(Parameters.RealPuzzleCount);
			WriteBytes(Parameters.ServerKey.ToBytes(), false);
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
