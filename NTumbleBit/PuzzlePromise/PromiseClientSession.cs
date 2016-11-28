using NBitcoin;
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
		class HashBase
		{
			public ServerCommitment Commitment
			{
				get;
				internal set;
			}
			public uint256 Hash
			{
				get; set;
			}
			public int Index
			{
				get; set;
			}
		}
		class RealHash : HashBase
		{
			public LockTime LockTime
			{
				get; set;
			}
		}

		class FakeHash : HashBase
		{
			public uint256 Salt
			{
				get; set;
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

			_ExpectedSigners = escrowCoin.GetScriptCode().GetDestinationPublicKeys();

			AssertState(PromiseClientStates.WaitingSignatureRequest);
			cashoutTransaction = cashoutTransaction.Clone();
			List<HashBase> hashes = new List<HashBase>();
			LockTime lockTime = new LockTime(0);
			for(int i = 0; i < Parameters.RealTransactionCount; i++)
			{
				RealHash h = new RealHash();
				cashoutTransaction.LockTime = lockTime;
				h.Hash = cashoutTransaction.GetSignatureHash(escrowCoin);
				h.LockTime = lockTime;
				lockTime++;
				hashes.Add(h);
			}

			for(int i = 0; i < Parameters.FakeTransactionCount; i++)
			{
				FakeHash h = new FakeHash();
				h.Salt = new uint256(RandomUtils.GetBytes(32));
				h.Hash = Parameters.CreateFakeHash(h.Salt);
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
				Hashes = _Hashes.Select(h => h.Hash).ToArray(),
				FakeIndexesHash = PromiseUtils.HashIndexes(ref indexSalt, _Hashes.OfType<FakeHash>().Select(h => h.Index))
			};
			_IndexSalt = indexSalt;
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

				var key = new SignatureKey(solution._Value);
				var signature = new ECDSASignature(key.XOR(fakeHash.Commitment.Promise));
				if(!_ExpectedSigners.Any(e => e.Verify(fakeHash.Hash, signature)))
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

			_State = PromiseClientStates.Completed;
			return _Hashes.OfType<RealHash>().First().Commitment.Puzzle;
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
