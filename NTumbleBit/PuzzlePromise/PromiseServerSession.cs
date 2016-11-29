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
	public enum PromiseServerStates
	{
		WaitingHashes,
		WaitingRevelation,
		Completed
	}

	public class PromiseServerSession
	{
		class EncryptedSignature
		{
			public EncryptedSignature(ECDSASignature ecdsa, uint256 signedHash, PuzzleSolution solution)
			{
				this.Signature = ecdsa;
				this.PuzzleSolution = solution;
				this.SignedHash = signedHash;
			}

			public uint256 SignedHash
			{
				get; set;
			}
			public ECDSASignature Signature
			{
				get; set;
			}

			public PuzzleSolution PuzzleSolution
			{
				get; set;
			}
		}
		public PromiseServerSession(RsaKey serverKey, PromiseParameters parameters = null)
		{
			if(serverKey == null)
				throw new ArgumentNullException("serverKey");
			_Parameters = parameters ?? new PromiseParameters();
			_Parameters.ServerKey = _Parameters.ServerKey ?? serverKey.PubKey;
			if(serverKey.PubKey != parameters.ServerKey)
				throw new ArgumentNullException("Private key not matching expected public key");
			_ServerKey = serverKey;
		}


		private Key _TransactionKey;
		private readonly RsaKey _ServerKey;
		EncryptedSignature[] _EncryptedSignatures;
		private readonly PromiseParameters _Parameters;
		private PromiseServerStates _State;

		public Key TransactionKey
		{
			get
			{
				return _TransactionKey;
			}
		}


		public RsaKey ServerKey
		{
			get
			{
				return _ServerKey;
			}
		}



		public PromiseParameters Parameters
		{
			get
			{
				return _Parameters;
			}
		}		

		public ServerCommitment[] SignHashes(SignaturesRequest sigRequest, Key transactionKey)
		{
			if(sigRequest == null)
				throw new ArgumentNullException("sigRequest");
			if(transactionKey == null)
				throw new ArgumentNullException("transactionKey");
			if(sigRequest.Hashes.Length != Parameters.GetTotalTransactionsCount())
				throw new ArgumentException("Incorrect number of hashes, expected " + sigRequest.Hashes.Length);
			AssertState(PromiseServerStates.WaitingHashes);
			List<ServerCommitment> promises = new List<ServerCommitment>();
			List<EncryptedSignature> encryptedSignatures = new List<EncryptedSignature>();
			foreach(var hash in sigRequest.Hashes)
			{
				var ecdsa = transactionKey.Sign(hash);
				var ecdsaDER = ecdsa.ToDER();
				var key = new SignatureKey(Utils.GenerateEncryptableInteger(Parameters.ServerKey._Key));
				var promise = key.XOR(ecdsaDER);
				PuzzleSolution solution = new PuzzleSolution(key.ToBytes());
				var puzzle = Parameters.ServerKey.GeneratePuzzle(ref solution);
				promises.Add(new ServerCommitment(puzzle.PuzzleValue, promise));
				encryptedSignatures.Add(new EncryptedSignature(ecdsa, hash, solution));
			}
			_TransactionKey = transactionKey;
			_State = PromiseServerStates.WaitingRevelation;
			_EncryptedSignatures = encryptedSignatures.ToArray();
			return promises.ToArray();
		}



		public ServerCommitmentsProof CheckRevelation(ClientRevelation revelation)
		{
			if(revelation == null)
				throw new ArgumentNullException("revelation");
			if(revelation.Salts.Length != Parameters.FakeTransactionCount || revelation.FakeIndexes.Length != Parameters.FakeTransactionCount)
				throw new ArgumentNullException("The revelation should contains " + Parameters.FakeTransactionCount + " indexes and salts");
			AssertState(PromiseServerStates.WaitingRevelation);
			List<PuzzleSolution> solutions = new List<PuzzleSolution>();
			for(int i = 0; i < Parameters.FakeTransactionCount; i++)
			{
				var salt = revelation.Salts[i];
				var encrypted = _EncryptedSignatures[revelation.FakeIndexes[i]];
				var actualSignedHash = Parameters.CreateFakeHash(salt);
				if(actualSignedHash != encrypted.SignedHash)
					throw new PuzzleException("Incorrect salt provided");
				solutions.Add(encrypted.PuzzleSolution);
			}

			// We can throw away the fake puzzles
			_EncryptedSignatures = _EncryptedSignatures
										.Where((e, i) => !revelation.FakeIndexes.Contains(i)).ToArray();

			Quotient[] quotients = new Quotient[Parameters.RealTransactionCount - 1];
			for(int i = 0; i < _EncryptedSignatures.Length - 1; i++)
			{
				var a = _EncryptedSignatures[i].PuzzleSolution._Value;
				var b = _EncryptedSignatures[i + 1].PuzzleSolution._Value;
				quotients[i] = new Quotient(b.Multiply(a.ModInverse(Parameters.ServerKey._Key.Modulus)).Mod(Parameters.ServerKey._Key.Modulus));
			}
			_State = PromiseServerStates.Completed;
			return new ServerCommitmentsProof(solutions.ToArray(), quotients);
		}


		public PromiseServerStates State
		{
			get
			{
				return _State;
			}
		}

		private void AssertState(PromiseServerStates state)
		{
			if(state != _State)
				throw new InvalidOperationException("Invalid state, actual " + _State + " while expected is " + state);
		}

	}
}
