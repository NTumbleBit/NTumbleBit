using NBitcoin;
using NTumbleBit.BouncyCastle.Math;
using NBitcoin.Crypto;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace NTumbleBit.PuzzlePromise
{
	public enum PromiseClientStates
	{
		WaitingSignatureRequest,
		WaitingCommitments,
		WaitingCommitmentsProof,
		Completed
	}

	public class PromiseClientSession
	{
		abstract class HashBase
		{
			public ServerCommitment Commitment
			{
				get;
				internal set;
			}
			public abstract uint256 GetHash();
			public int Index
			{
				get; set;
			}
		}
		class RealHash : HashBase
		{
			public RealHash(Transaction tx, ScriptCoin coin)
			{
				_BaseTransaction = tx;
				_Escrow = coin;
			}
			private readonly ScriptCoin _Escrow;
			private readonly Transaction _BaseTransaction;
			public LockTime LockTime
			{
				get; set;
			}

			public override uint256 GetHash()
			{
				return GetTransaction().GetSignatureHash(_Escrow);
			}

			public Transaction GetTransaction()
			{
				var clone = _BaseTransaction.Clone();
				clone.LockTime = LockTime;
				return clone;
			}
		}

		class FakeHash : HashBase
		{
			public FakeHash(PromiseParameters parameters)
			{
				if(parameters == null)
					throw new ArgumentNullException("parameters");
				Parameters = parameters;
			}
			public uint256 Salt
			{
				get; set;
			}
			public PromiseParameters Parameters
			{
				get;
				private set;
			}
			public override uint256 GetHash()
			{
				return Parameters.CreateFakeHash(Salt);
			}
		}

		public PromiseClientSession(PromiseParameters parameters = null)
		{
			_Parameters = parameters ?? new PromiseParameters();
		}

		public PromiseParameters Parameters
		{
			get
			{
				return _Parameters;
			}
		}

		public byte[] ToBytes()
		{
			MemoryStream ms = new MemoryStream();
			WriteTo(ms);
			ms.Position = 0;
			return ms.ToArrayEfficient();
		}
		public static PromiseClientSession ReadFrom(Stream stream)
		{
			if(stream == null)
				throw new ArgumentNullException("stream");

			var text = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
			JsonSerializerSettings settings = new JsonSerializerSettings();
			Serializer.RegisterFrontConverters(settings);
			var state = JsonConvert.DeserializeObject<InternalState>(text, settings);
			return new PromiseClientSession(state);
		}
		public void WriteTo(Stream stream)
		{
			if(stream == null)
				throw new ArgumentNullException("stream");
			var writer = new StreamWriter(stream, Encoding.UTF8);
			JsonSerializerSettings settings = new JsonSerializerSettings();
			Serializer.RegisterFrontConverters(settings);
			var result = JsonConvert.SerializeObject(GetInternalState(), settings);
			writer.Write(result);
			writer.Flush();
		}

		public class InternalState
		{
			public Transaction Cashout
			{
				get;
				set;
			}
			public ServerCommitment[] Commitments
			{
				get;
				set;
			}
			public ScriptCoin EscrowedCoin
			{
				get;
				set;
			}
			public uint256[] FakeSalts
			{
				get;
				set;
			}
			public uint256 IndexSalt
			{
				get;
				set;
			}
			public LockTime[] LockTimes
			{
				get;
				set;
			}
			public PromiseParameters PromiseParameters
			{
				get;
				set;
			}
			public Quotient[] Quotients
			{
				get;
				set;
			}
			public PromiseClientStates State
			{
				get;
				set;
			}
			public int[] FakeIndexes
			{
				get; set;
			}
		}

		private readonly PromiseParameters _Parameters;
		private PromiseClientStates _State;
		Quotient[] _Quotients;
		private uint256 _IndexSalt;
		private HashBase[] _Hashes;
		ScriptCoin _EscrowedCoin;
		Transaction _Cashout;
		private int[] _FakeIndexes;

		public PromiseClientSession(InternalState state)
		{
			if(state == null)
				throw new ArgumentNullException("state");

			_Parameters = state.PromiseParameters;
			_Quotients = state.Quotients;
			_State = state.State;
			_IndexSalt = state.IndexSalt;
			_EscrowedCoin = state.EscrowedCoin;
			_Cashout = state.Cashout;
			_FakeIndexes = state.FakeIndexes;
			if(state.Commitments != null)
			{
				_Hashes = new HashBase[state.Commitments.Length];
				int fakeI = 0, realI = 0;
				for(int i = 0; i < _Hashes.Length; i++)
				{
					HashBase hash = null;
					if(_FakeIndexes != null && _FakeIndexes.Contains(i))
					{
						hash = new FakeHash(_Parameters)
						{
							Salt = state.FakeSalts[fakeI++]
						};
					}
					else
					{
						hash = new RealHash(_Cashout, _EscrowedCoin)
						{
							LockTime = state.LockTimes[realI++]
						};
					}
					hash.Index = i;
					hash.Commitment = state.Commitments[i];
					_Hashes[i] = hash;
				}
			}
		}

		private InternalState GetInternalState()
		{
			InternalState state = new InternalState();
			state.PromiseParameters = _Parameters;
			state.Quotients = _Quotients;
			state.State = State;
			state.IndexSalt = _IndexSalt;
			state.EscrowedCoin = _EscrowedCoin;
			state.Cashout = _Cashout;
			state.FakeIndexes = _FakeIndexes;
			if(_Hashes != null)
			{
				var commitments = new List<ServerCommitment>();
				var fakeSalts = new List<uint256>();
				var lockTimes = new List<LockTime>();
				for(int i = 0; i < _Hashes.Length; i++)
				{
					commitments.Add(_Hashes[i].Commitment);
					var fake = _Hashes[i] as FakeHash;
					if(fake != null)
					{
						fakeSalts.Add(fake.Salt);
					}

					var real = _Hashes[i] as RealHash;
					if(real != null)
					{
						lockTimes.Add(real.LockTime);
					}
				}
				state.FakeSalts = fakeSalts.ToArray();
				state.LockTimes = lockTimes.ToArray();
				state.Commitments = commitments.ToArray();
			}
			return state;
		}

		public static PromiseClientSession ReadFrom(byte[] bytes)
		{
			if(bytes == null)
				throw new ArgumentNullException("bytes");
			var ms = new MemoryStream(bytes);
			return ReadFrom(ms);
		}

		public SignaturesRequest CreateSignatureRequest(ScriptCoin escrowedCoin, Transaction cashout)
		{
			if(escrowedCoin == null)
				throw new ArgumentNullException("escrowedCoin");
			if(cashout == null)
				throw new ArgumentNullException("cashout");
			AssertState(PromiseClientStates.WaitingSignatureRequest);
			List<HashBase> hashes = new List<HashBase>();
			LockTime lockTime = new LockTime(0);
			for(int i = 0; i < Parameters.RealTransactionCount; i++)
			{
				RealHash h = new RealHash(cashout, escrowedCoin);
				h.LockTime = lockTime;
				lockTime++;
				hashes.Add(h);
			}

			for(int i = 0; i < Parameters.FakeTransactionCount; i++)
			{
				FakeHash h = new FakeHash(Parameters);
				h.Salt = new uint256(RandomUtils.GetBytes(32));
				hashes.Add(h);
			}

			_Hashes = hashes.ToArray();
			NBitcoin.Utils.Shuffle(_Hashes, RandomUtils.GetInt32());
			for(int i = 0; i < _Hashes.Length; i++)
			{
				_Hashes[i].Index = i;
			}
			var fakeIndices = _Hashes.OfType<FakeHash>().Select(h => h.Index).ToArray();
			uint256 indexSalt = null;
			var request = new SignaturesRequest()
			{
				Hashes = _Hashes.Select(h => h.GetHash()).ToArray(),
				FakeIndexesHash = PromiseUtils.HashIndexes(ref indexSalt, fakeIndices),
			};
			_IndexSalt = indexSalt;
			_Cashout = cashout.Clone();
			_State = PromiseClientStates.WaitingCommitments;
			_FakeIndexes = fakeIndices;
			_EscrowedCoin = escrowedCoin;
			return request;
		}

		PubKey[] GetExpectedSigners()
		{
			var multiSig = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(_EscrowedCoin.GetScriptCode());
			if(multiSig == null || multiSig.SignatureCount != 2 || multiSig.InvalidPubKeys.Length != 0 || multiSig.PubKeys.Length != 2)
				throw new ArgumentException("Invalid escrow 2-2 multisig");
			return multiSig.PubKeys;
		}

		public ClientRevelation Reveal(ServerCommitment[] commitments)
		{
			if(commitments == null)
				throw new ArgumentNullException("commitments");
			if(commitments.Length != Parameters.GetTotalTransactionsCount())
				throw new ArgumentException("Expecting " + Parameters.GetTotalTransactionsCount() + " commitments");
			AssertState(PromiseClientStates.WaitingCommitments);

			List<uint256> salts = new List<uint256>();
			List<int> indexes = new List<int>();
			foreach(var fakeHash in _Hashes.OfType<FakeHash>())
			{
				salts.Add(fakeHash.Salt);
				indexes.Add(fakeHash.Index);
			}

			for(int i = 0; i < commitments.Length; i++)
			{
				_Hashes[i].Commitment = commitments[i];
			}
			_State = PromiseClientStates.WaitingCommitmentsProof;
			return new ClientRevelation(indexes.ToArray(), _IndexSalt, salts.ToArray());
		}

		public PuzzleValue CheckCommitmentProof(ServerCommitmentsProof proof)
		{
			if(proof == null)
				throw new ArgumentNullException("proof");
			if(proof.FakeSolutions.Length != Parameters.FakeTransactionCount)
				throw new ArgumentException("Expecting " + Parameters.FakeTransactionCount + " solutions");
			if(proof.Quotients.Length != Parameters.RealTransactionCount - 1)
				throw new ArgumentException("Expecting " + (Parameters.RealTransactionCount - 1) + " quotients");
			AssertState(PromiseClientStates.WaitingCommitmentsProof);

			var fakeHashes = _Hashes.OfType<FakeHash>().ToArray();
			for(int i = 0; i < proof.FakeSolutions.Length; i++)
			{
				var fakeHash = fakeHashes[i];
				var solution = proof.FakeSolutions[i];

				if(solution._Value.CompareTo(Parameters.ServerKey._Key.Modulus) >= 0)
					throw new PuzzleException("Solution bigger than modulus");

				if(!new Puzzle(Parameters.ServerKey, fakeHash.Commitment.Puzzle).Verify(solution))
					throw new PuzzleException("Invalid puzzle solution");

				PubKey signer;
				ECDSASignature sig;
				if(!IsValidSignature(solution, fakeHash, out signer, out sig))
					throw new PuzzleException("Invalid ECDSA signature");
			}


			var realHashes = _Hashes.OfType<RealHash>().ToArray();
			for(int i = 1; i < Parameters.RealTransactionCount; i++)
			{
				var q = proof.Quotients[i - 1]._Value;
				var p1 = realHashes[i - 1].Commitment.Puzzle._Value;
				var p2 = realHashes[i].Commitment.Puzzle._Value;
				var p22 = p1.Multiply(Parameters.ServerKey.Encrypt(q)).Mod(Parameters.ServerKey._Key.Modulus);
				if(!p2.Equals(p22))
					throw new PuzzleException("Invalid quotient");
			}

			_Hashes = _Hashes.OfType<RealHash>().ToArray(); // we do not need the fake one anymore
			_FakeIndexes = null;
			_Quotients = proof.Quotients;
			_State = PromiseClientStates.Completed;
			return _Hashes.OfType<RealHash>().First().Commitment.Puzzle;
		}

		private bool IsValidSignature(PuzzleSolution solution, HashBase hash, out PubKey signer, out ECDSASignature signature)
		{
			signer = null;
			signature = null;
			try
			{
				var key = new XORKey(solution);
				signature = new ECDSASignature(key.XOR(hash.Commitment.Promise));
				foreach(var sig in GetExpectedSigners())
				{
					if(sig.Verify(hash.GetHash(), signature))
					{
						signer = sig;
						return true;
					}
				}
				return false;
			}
			catch
			{
			}
			return false;
		}


		internal IEnumerable<Transaction> GetSignedTransactions(PuzzleSolution solution)
		{
			if(solution == null)
				throw new ArgumentNullException("solution");
			BigInteger cumul = solution._Value;
			var hashes = _Hashes.OfType<RealHash>().ToArray();
			for(int i = 0; i < Parameters.RealTransactionCount; i++)
			{
				var hash = hashes[i];
				var quotient = i == 0 ? BigInteger.One : _Quotients[i - 1]._Value;
				cumul = cumul.Multiply(quotient).Mod(Parameters.ServerKey._Key.Modulus);
				solution = new PuzzleSolution(cumul);
				ECDSASignature signature;
				PubKey signer;
				if(!IsValidSignature(solution, hash, out signer, out signature))
					continue;

				var transaction = hash.GetTransaction();
				TransactionBuilder txBuilder = new TransactionBuilder();
				txBuilder.AddCoins(_EscrowedCoin);
				txBuilder.AddKnownSignature(signer, signature);
				txBuilder.SignTransactionInPlace(transaction);
				yield return transaction;
			}
		}

		public Transaction GetSignedTransaction(PuzzleSolution solution)
		{
			var tx = GetSignedTransactions(solution).FirstOrDefault();
			if(tx == null)
				throw new PuzzleException("Wrong solution for the puzzle");
			return tx;
		}

		public PromiseClientStates State
		{
			get
			{
				return _State;
			}
		}

		private void AssertState(PromiseClientStates state)
		{
			if(state != _State)
				throw new InvalidOperationException("Invalid state, actual " + _State + " while expected is " + state);
		}
	}
}
