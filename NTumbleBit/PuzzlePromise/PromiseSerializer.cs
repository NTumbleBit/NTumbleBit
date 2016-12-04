using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
	public class PromiseSerializer : SerializerBase
	{
		public PromiseSerializer(PromiseParameters parameters, Stream inner) : base(inner)
		{
			if(inner == null)
				throw new ArgumentNullException("inner");
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			_Parameters = parameters;
		}


		private readonly PromiseParameters _Parameters;
		public PromiseParameters Parameters
		{
			get
			{
				return _Parameters;
			}
		}

		public void WriteParameters()
		{
			WriteUInt(Parameters.FakeTransactionCount);
			WriteUInt(Parameters.RealTransactionCount);
			WriteBytes(Parameters.ServerKey.ToBytes(), false);
		}

		public PromiseParameters ReadParameters()
		{
			int fakeTxCount, realTxCount;
			fakeTxCount = (int)ReadUInt();
			realTxCount = (int)ReadUInt();

			var bytes = ReadBytes(-1, 10 * 1024);
			var key = new RsaPubKey(bytes);
			return new PromiseParameters(key) { FakeTransactionCount = fakeTxCount, RealTransactionCount = realTxCount };
		}

		public void WriteQuotients(Quotient[] quotients)
		{
			if(quotients == null)
				throw new ArgumentNullException("quotients");
			if(quotients.Length != Parameters.RealTransactionCount - 1)
				throw new ArgumentException("Invalid quotients array size");
			for(int i = 0; i < quotients.Length; i++)
			{
				WriteQuotient(quotients[i]);
			}
		}

		public void WriteQuotient(Quotient q)
		{
			WriteBigInteger(q._Value, KeySize);
		}

		public Quotient[] ReadQuotients()
		{
			var result = new Quotient[Parameters.RealTransactionCount - 1];
			for(int i = 0; i < result.Length; i++)
			{
				result[i] = ReadQuotient();
			}
			return result;
		}

		public Quotient ReadQuotient()
		{
			return new Quotient(ReadBigInteger(KeySize));
		}

		public void WriteSignaturesRequest(SignaturesRequest signatureRequest)
		{
			if(signatureRequest == null)
				throw new ArgumentNullException("signatureRequest");
			if(signatureRequest.Hashes.Length != Parameters.GetTotalTransactionsCount())
				throw new ArgumentException("Invalid hashes array size");
			for(int i = 0; i < Parameters.GetTotalTransactionsCount(); i++)
			{
				WriteUInt256(signatureRequest.Hashes[i]);
			}
			WriteUInt256(signatureRequest.FakeIndexesHash);
		}

		public SignaturesRequest ReadSignaturesRequest()
		{
			var req = new SignaturesRequest();
			req.Hashes = new uint256[Parameters.GetTotalTransactionsCount()];
			for(int i = 0; i < Parameters.GetTotalTransactionsCount(); i++)
			{
				req.Hashes[i] = ReadUInt256();
			}
			return req;
		}

		public void WriteCommitments(ServerCommitment[] commitments)
		{
			if(commitments == null)
				throw new ArgumentNullException("commitments");
			if(commitments.Length != Parameters.GetTotalTransactionsCount())
				throw new ArgumentException("Invalid commitments lenght");

			for(int i = 0; i < Parameters.GetTotalTransactionsCount(); i++)
			{
				WriteCommitment(commitments[i]);
			}
		}

		public ServerCommitment[] ReadCommitments()
		{
			ServerCommitment[] commitments = new ServerCommitment[Parameters.GetTotalTransactionsCount()];
			for(int i = 0; i < commitments.Length; i++)
			{
				commitments[i] = ReadCommitment();
			}
			return commitments;
		}

		public void WriteCommitment(ServerCommitment commitment)
		{
			WritePuzzle(commitment.Puzzle);
			WriteBytes(commitment.Promise, false);
		}

		public ServerCommitment ReadCommitment()
		{
			var puzzle = ReadPuzzle();
			var promise = ReadBytes();
			return new ServerCommitment(puzzle, promise);
		}

		public uint256 ReadUInt256()
		{
			return new uint256(ReadBytes(32), littleEndian);
		}

		public void WriteUInt256(uint256 hash)
		{
			WriteBytes(hash.ToBytes(littleEndian), littleEndian);
		}

		public ScriptCoin ReadScriptCoin()
		{
			var txId = ReadUInt256();
			var index = ReadUInt();
			var money = Money.Satoshis(ReadULong());
			var scriptPubKey = new Script(ReadBytes());
			var redeem = new Script(ReadBytes());
			return new ScriptCoin(txId, (uint)index, money, scriptPubKey, redeem);
		}
		public void WriteScriptCoin(ScriptCoin coin)
		{
			WriteUInt256(coin.Outpoint.Hash);
			WriteUInt(coin.Outpoint.N);
			WriteULong((ulong)coin.Amount.Satoshi);
			WriteBytes(coin.ScriptPubKey.ToBytes(), false);
			WriteBytes(coin.Redeem.ToBytes(), false);
		}

		public void WriteRevelation(ClientRevelation revelation)
		{
			if(revelation == null)
				throw new ArgumentNullException("revelation");
			if(revelation.FakeIndexes.Length != Parameters.FakeTransactionCount)
				throw new ArgumentException("Incorrect index count");
			if(revelation.Salts.Length != Parameters.FakeTransactionCount)
				throw new ArgumentException("Incorrect salts count");
			foreach(var index in revelation.FakeIndexes)
				WriteUInt(index);
			foreach(var salt in revelation.Salts)
				WriteUInt256(salt);
		}

		public ClientRevelation ReadRevelation()
		{
			int[] fakeIndexes = new int[Parameters.FakeTransactionCount];
			uint256[] salt = new uint256[Parameters.FakeTransactionCount];
			for(int i = 0; i < fakeIndexes.Length; i++)
			{
				fakeIndexes[i] = (int)ReadUInt();
			}
			for(int i = 0; i < salt.Length; i++)
			{
				salt[i] = ReadUInt256();
			}
			return new ClientRevelation(fakeIndexes, salt);
		}

		public void WriteCommitmentsProof(ServerCommitmentsProof proof)
		{
			if(proof == null)
				throw new ArgumentNullException("proof");
			if(proof.FakeSolutions.Length != Parameters.FakeTransactionCount)
				throw new ArgumentException("Incorrect FakeSolutions count");
			if(proof.Quotients.Length != Parameters.RealTransactionCount - 1)
				throw new ArgumentException("Incorrect Quotients count");

			foreach(var solution in proof.FakeSolutions)
			{
				WritePuzzleSolution(solution);
			}

			foreach(var q in proof.Quotients)
			{
				WriteQuotient(q);
			}
		}

		public ServerCommitmentsProof ReadCommitmentsProof()
		{
			PuzzleSolution[] solutions = new PuzzleSolution[Parameters.FakeTransactionCount];
			for(int i = 0; i < Parameters.FakeTransactionCount; i++)
			{
				solutions[i] = ReadPuzzleSolution();
			}

			Quotient[] quotients = new Quotient[Parameters.RealTransactionCount - 1];
			for(int i = 0; i < quotients.Length; i++)
			{
				quotients[i] = ReadQuotient();
			}
			return new ServerCommitmentsProof(solutions, quotients);
		}
	}
}
