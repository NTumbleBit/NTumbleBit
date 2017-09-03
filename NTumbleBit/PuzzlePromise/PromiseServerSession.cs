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
using NTumbleBit.ClassicTumbler;

namespace NTumbleBit.PuzzlePromise
{
	public enum PromiseServerStates
	{
		WaitingEscrow,
		WaitingHashes,
		WaitingRevelation,
		Completed
	}

	public class PromiseServerSession : EscrowInitiator
	{
		public class EncryptedSignature
		{
			public EncryptedSignature()
			{

			}
			public EncryptedSignature(ECDSASignature ecdsa, uint256 signedHash, PuzzleSolution solution)
			{
				Signature = ecdsa;
				PuzzleSolution = solution;
				SignedHash = signedHash;
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
		public PromiseServerSession(PromiseParameters parameters)
		{
			if(parameters == null)
				throw new ArgumentNullException(nameof(parameters));
			_Parameters = parameters;
			InternalState = new State();
		}

		public PromiseServerSession(State state, PromiseParameters parameters) : this(parameters)
		{
			if(state == null)
				throw new ArgumentNullException(nameof(state));
			InternalState = state;
		}

		public new class State : EscrowInitiator.State
		{
			public EncryptedSignature[] EncryptedSignatures
			{
				get; set;
			}

			public PromiseServerStates Status
			{
				get; set;
			}
			public uint256 FakeIndexesHash
			{
				get;
				set;
			}
			public int ETag
			{
				get;
				set;
			}
		}

		public State GetInternalState()
		{
			var state = Serializer.Clone(InternalState);
			return state;
		}

		protected new State InternalState
		{
			get
			{
				return (State)base.InternalState;
			}
			set
			{
				base.InternalState = value;
			}
		}


		private readonly PromiseParameters _Parameters;
		public PromiseParameters Parameters
		{
			get
			{
				return _Parameters;
			}
		}

		public override void ConfigureEscrowedCoin(uint160 channelId, ScriptCoin escrowedCoin, Key escrowKey, Script redeemDestination)
		{
			AssertState(PromiseServerStates.WaitingEscrow);
			base.ConfigureEscrowedCoin(channelId, escrowedCoin, escrowKey, redeemDestination);
			InternalState.Status = PromiseServerStates.WaitingHashes;
		}


		public ServerCommitment[] SignHashes(SignaturesRequest sigRequest)
		{
			if(sigRequest == null)
				throw new ArgumentNullException(nameof(sigRequest));
			if(sigRequest.Hashes.Length != Parameters.GetTotalTransactionsCount())
				throw new ArgumentException("Incorrect number of hashes, expected " + sigRequest.Hashes.Length);
			AssertState(PromiseServerStates.WaitingHashes);
			List<ServerCommitment> promises = new List<ServerCommitment>();
			List<EncryptedSignature> encryptedSignatures = new List<EncryptedSignature>();
			foreach(var hash in sigRequest.Hashes)
			{
				var ecdsa = InternalState.EscrowKey.Sign(hash);
				var ecdsaDER = ecdsa.ToDER();
				var key = new XORKey(Parameters.ServerKey);
				var promise = key.XOR(ecdsaDER);
				PuzzleSolution solution = new PuzzleSolution(key.ToBytes());
				var puzzle = Parameters.ServerKey.GeneratePuzzle(ref solution);
				promises.Add(new ServerCommitment(puzzle.PuzzleValue, promise));
				encryptedSignatures.Add(new EncryptedSignature(ecdsa, hash, solution));
			}
			InternalState.Status = PromiseServerStates.WaitingRevelation;
			InternalState.EncryptedSignatures = encryptedSignatures.ToArray();
			InternalState.FakeIndexesHash = sigRequest.FakeIndexesHash;
			return promises.ToArray();
		}



		public ServerCommitmentsProof CheckRevelation(ClientRevelation revelation)
		{
			if(revelation == null)
				throw new ArgumentNullException(nameof(revelation));
			if(revelation.Salts.Length != Parameters.FakeTransactionCount || revelation.FakeIndexes.Length != Parameters.FakeTransactionCount)
				throw new ArgumentNullException("The revelation should contains " + Parameters.FakeTransactionCount + " indexes and salts");
			AssertState(PromiseServerStates.WaitingRevelation);

			var indexSalt = revelation.IndexesSalt;
			if(InternalState.FakeIndexesHash != PromiseUtils.HashIndexes(ref indexSalt, revelation.FakeIndexes))
			{
				throw new PuzzleException("Invalid index salt");
			}

			List<PuzzleSolution> solutions = new List<PuzzleSolution>();
			for(int i = 0; i < Parameters.FakeTransactionCount; i++)
			{
				var salt = revelation.Salts[i];
				var encrypted = InternalState.EncryptedSignatures[revelation.FakeIndexes[i]];
				var actualSignedHash = Parameters.CreateFakeHash(salt);
				if(actualSignedHash != encrypted.SignedHash)
					throw new PuzzleException("Incorrect salt provided");
				solutions.Add(encrypted.PuzzleSolution);
			}

			// We can throw away the fake puzzles
			InternalState.EncryptedSignatures = InternalState.EncryptedSignatures
										.Where((e, i) => !revelation.FakeIndexes.Contains(i)).ToArray();

			Quotient[] quotients = new Quotient[Parameters.RealTransactionCount - 1];
			for(int i = 0; i < InternalState.EncryptedSignatures.Length - 1; i++)
			{
				var a = InternalState.EncryptedSignatures[i].PuzzleSolution._Value;
				var b = InternalState.EncryptedSignatures[i + 1].PuzzleSolution._Value;
				quotients[i] = new Quotient(b.Multiply(a.ModInverse(Parameters.ServerKey._Key.Modulus)).Mod(Parameters.ServerKey._Key.Modulus));
			}
			InternalState.FakeIndexesHash = null;
			InternalState.Status = PromiseServerStates.Completed;
			return new ServerCommitmentsProof(solutions.ToArray(), quotients);
		}


		public PromiseServerStates Status
		{
			get
			{
				return InternalState.Status;
			}
		}

		private void AssertState(PromiseServerStates state)
		{
			if(state != InternalState.Status)
				throw new InvalidStateException("Invalid state, actual " + InternalState.Status + " while expected is " + state);
		}

		public override LockTime GetLockTime(CycleParameters cycle)
		{
			return cycle.GetTumblerLockTime();
		}
	}
}
