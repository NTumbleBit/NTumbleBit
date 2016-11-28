using NBitcoin;
using NTumbleBit.BouncyCastle.Math;
using NBitcoin.Crypto;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
			private Transaction cashoutTransaction;
			private ICoin escrowCoin;

			public RealHash(ICoin escrowCoin, Transaction cashoutTransaction)
			{
				this.escrowCoin = escrowCoin;
				this.cashoutTransaction = cashoutTransaction;
			}

			public LockTime LockTime
			{
				get; set;
			}

			public override uint256 GetHash()
			{
				Transaction clone = GetTransaction();
				return clone.GetSignatureHash(escrowCoin);
			}

			public Transaction GetTransaction()
			{
				var clone = cashoutTransaction.Clone();
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


		private readonly PromiseParameters _Parameters;
		public PromiseParameters Parameters
		{
			get
			{
				return _Parameters;
			}
		}

		private HashBase[] _Hashes;
		private uint256 _IndexSalt;
		private PubKey[] _ExpectedSigners;

		public SignaturesRequest CreateSignatureRequest(ICoin escrowCoin, Transaction cashoutTransaction)
		{
			if(escrowCoin == null)
				throw new ArgumentNullException("escrowCoin");
			if(cashoutTransaction == null)
				throw new ArgumentNullException("cashoutTransaction");

			var multiSig = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(escrowCoin.GetScriptCode());
			if(multiSig == null || multiSig.SignatureCount != 2 || multiSig.InvalidPubKeys.Length != 0 || multiSig.PubKeys.Length != 2)
				throw new ArgumentException("Invalid escrow 2-2 multisig");
			AssertState(PromiseClientStates.WaitingSignatureRequest);
			cashoutTransaction = cashoutTransaction.Clone();
			List<HashBase> hashes = new List<HashBase>();
			LockTime lockTime = new LockTime(0);
			for(int i = 0; i < Parameters.RealTransactionCount; i++)
			{
				RealHash h = new RealHash(escrowCoin, cashoutTransaction);
				cashoutTransaction.LockTime = lockTime;
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
			uint256 indexSalt = null;
			var request = new SignaturesRequest()
			{
				Hashes = _Hashes.Select(h => h.GetHash()).ToArray(),
				FakeIndexesHash = PromiseUtils.HashIndexes(ref indexSalt, _Hashes.OfType<FakeHash>().Select(h => h.Index))
			};
			_IndexSalt = indexSalt;
			_ExpectedSigners = multiSig.PubKeys;
			_State = PromiseClientStates.WaitingCommitments;
			return request;
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
			return new ClientRevelation(indexes.ToArray(), salts.ToArray());
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
				var key = new SignatureKey(solution._Value);
				signature = new ECDSASignature(key.XOR(hash.Commitment.Promise));
				foreach(var sig in _ExpectedSigners)
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

		Quotient[] _Quotients;

		internal IEnumerable<Transaction> GetSignedTransactions(PuzzleSolution solution, ICoin escrowCoin)
		{
			if(escrowCoin == null)
				throw new ArgumentNullException("escrowCoin");
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
				txBuilder.AddCoins(escrowCoin);
				txBuilder.AddKnownSignature(signer, signature);
				txBuilder.SignTransactionInPlace(transaction);
				yield return transaction;
			}
		}

		public Transaction GetSignedTransaction(PuzzleSolution solution, ICoin escrowCoin)
		{
			var tx = GetSignedTransactions(solution, escrowCoin).FirstOrDefault();
			if(tx == null)
				throw new PuzzleException("Wrong solution for the puzzle");
			return tx;
		}

		private PromiseClientStates _State;
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
