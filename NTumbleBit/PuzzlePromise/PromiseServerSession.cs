using NBitcoin;
using NBitcoin.Crypto;
using Newtonsoft.Json;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.IO;
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
			_InternalState.Parameters = parameters ?? new PromiseParameters();
			_InternalState.Parameters.ServerKey = _InternalState.Parameters.ServerKey ?? serverKey.PubKey;
			if(serverKey.PubKey != parameters.ServerKey)
				throw new ArgumentNullException("Private key not matching expected public key");
			_InternalState.ServerKey = serverKey;
		}

		PromiseServerSession(InternalState state)
		{
			if(state == null)
				throw new ArgumentNullException("state");
			this._InternalState = state;
		}

		public static PromiseServerSession ReadFrom(byte[] bytes, RsaKey rsaKey = null, Key ecdsaKey = null)
		{
			if(bytes == null)
				throw new ArgumentNullException("bytes");
			var ms = new MemoryStream(bytes);
			return ReadFrom(ms);
		}
		public static PromiseServerSession ReadFrom(Stream stream, RsaKey rsaKey = null, Key ecdsaKey = null)
		{
			if(stream == null)
				throw new ArgumentNullException("stream");
			if(stream == null)
				throw new ArgumentNullException("stream");

			var text = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
			JsonSerializerSettings settings = new JsonSerializerSettings();
			Serializer.RegisterFrontConverters(settings);
			var state = JsonConvert.DeserializeObject<InternalState>(text, settings);
			state.ServerKey = state.ServerKey ?? rsaKey;
			state.TransactionKey = state.TransactionKey ?? ecdsaKey;
			return new PromiseServerSession(state);
		}

		public byte[] ToBytes(bool includePrivateKeys)
		{
			MemoryStream ms = new MemoryStream();
			WriteTo(ms, includePrivateKeys);
			ms.Position = 0;
			return ms.ToArrayEfficient();
		}
		public void WriteTo(Stream stream, bool includePrivateKeys)
		{
			if(stream == null)
				throw new ArgumentNullException("stream");
			var writer = new StreamWriter(stream, Encoding.UTF8);
			JsonSerializerSettings settings = new JsonSerializerSettings();
			Serializer.RegisterFrontConverters(settings);
			var result = JsonConvert.SerializeObject(this._InternalState, settings);
			var key = _InternalState.ServerKey;
			var ecdsaKey = _InternalState.TransactionKey;
			if(!includePrivateKeys)
			{
				_InternalState.ServerKey = null;
			}
			writer.Write(result);
			_InternalState.ServerKey = key;
			_InternalState.TransactionKey = ecdsaKey;
			writer.Flush();
		}

		class InternalState
		{
			public Key TransactionKey
			{
				get; set;
			}
			public RsaKey ServerKey
			{
				get; set;
			}
			public EncryptedSignature[] EncryptedSignatures
			{
				get; set;
			}

			public PromiseParameters Parameters
			{
				get; set;
			}

			public PromiseServerStates State
			{
				get; set;
			}
			public uint256 FakeIndexesHash
			{
				get;
				set;
			}
		}

		InternalState _InternalState = new InternalState();

		public Key TransactionKey
		{
			get
			{
				return _InternalState.TransactionKey;
			}
		}


		public RsaKey ServerKey
		{
			get
			{
				return _InternalState.ServerKey;
			}
		}


		public PromiseParameters Parameters
		{
			get
			{
				return _InternalState.Parameters;
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
			_InternalState.TransactionKey = transactionKey;
			_InternalState.State = PromiseServerStates.WaitingRevelation;
			_InternalState.EncryptedSignatures = encryptedSignatures.ToArray();
			_InternalState.FakeIndexesHash = sigRequest.FakeIndexesHash;
			return promises.ToArray();
		}



		public ServerCommitmentsProof CheckRevelation(ClientRevelation revelation)
		{
			if(revelation == null)
				throw new ArgumentNullException("revelation");
			if(revelation.Salts.Length != Parameters.FakeTransactionCount || revelation.FakeIndexes.Length != Parameters.FakeTransactionCount)
				throw new ArgumentNullException("The revelation should contains " + Parameters.FakeTransactionCount + " indexes and salts");
			AssertState(PromiseServerStates.WaitingRevelation);

			var indexSalt = revelation.IndexesSalt;
			if(_InternalState.FakeIndexesHash != PromiseUtils.HashIndexes(ref indexSalt, revelation.FakeIndexes))
			{
				throw new PuzzleException("Invalid index salt");
			}

			List<PuzzleSolution> solutions = new List<PuzzleSolution>();
			for(int i = 0; i < Parameters.FakeTransactionCount; i++)
			{
				var salt = revelation.Salts[i];
				var encrypted = _InternalState.EncryptedSignatures[revelation.FakeIndexes[i]];
				var actualSignedHash = Parameters.CreateFakeHash(salt);
				if(actualSignedHash != encrypted.SignedHash)
					throw new PuzzleException("Incorrect salt provided");
				solutions.Add(encrypted.PuzzleSolution);
			}

			// We can throw away the fake puzzles
			_InternalState.EncryptedSignatures = _InternalState.EncryptedSignatures
										.Where((e, i) => !revelation.FakeIndexes.Contains(i)).ToArray();

			Quotient[] quotients = new Quotient[Parameters.RealTransactionCount - 1];
			for(int i = 0; i < _InternalState.EncryptedSignatures.Length - 1; i++)
			{
				var a = _InternalState.EncryptedSignatures[i].PuzzleSolution._Value;
				var b = _InternalState.EncryptedSignatures[i + 1].PuzzleSolution._Value;
				quotients[i] = new Quotient(b.Multiply(a.ModInverse(Parameters.ServerKey._Key.Modulus)).Mod(Parameters.ServerKey._Key.Modulus));
			}
			_InternalState.FakeIndexesHash = null;
			_InternalState.State = PromiseServerStates.Completed;
			return new ServerCommitmentsProof(solutions.ToArray(), quotients);
		}


		public PromiseServerStates State
		{
			get
			{
				return _InternalState.State;
			}
		}

		private void AssertState(PromiseServerStates state)
		{
			if(state != _InternalState.State)
				throw new InvalidOperationException("Invalid state, actual " + _InternalState.State + " while expected is " + state);
		}		
	}
}
