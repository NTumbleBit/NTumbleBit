using NBitcoin;
using NBitcoin.Crypto;
using NTumbleBit.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace NTumbleBit.PuzzleSolver
{
	public enum SolverServerStates
	{
		WaitingPuzzles,
		WaitingRevelation,
		WaitingBlindFactor,
		Completed
	}
	public class SolverServerSession
	{
		class SolvedPuzzle
		{
			public SolvedPuzzle(Puzzle puzzle, SolutionKey key, PuzzleSolution solution)
			{
				Puzzle = puzzle;
				SolutionKey = key;
				Solution = solution;
			}

			public Puzzle Puzzle
			{
				get; set;
			}
			public SolutionKey SolutionKey
			{
				get; set;
			}
			public PuzzleSolution Solution
			{
				get; set;
			}
		}
		public SolverServerSession(RsaKey serverKey) : this(serverKey, null)
		{
		}

		public SolverServerSession(RsaKey serverKey, SolverParameters parameters)
		{
			parameters = parameters ?? new SolverParameters(serverKey.PubKey);
			if(serverKey == null)
				throw new ArgumentNullException("serverKey");
			if(serverKey.PubKey != parameters.ServerKey)
				throw new ArgumentNullException("Private key not matching expected public key");
			_ServerKey = serverKey;
			_Parameters = parameters;
		}

		private readonly SolverParameters _Parameters;
		private readonly RsaKey _ServerKey;
		private SolverServerStates _State = SolverServerStates.WaitingPuzzles;
		private SolvedPuzzle[] _SolvedPuzzles = new SolvedPuzzle[0];

		public static SolverServerSession ReadFrom(byte[] bytes, RsaKey privateKey)
		{
			if(bytes == null)
				throw new ArgumentNullException("bytes");
			var ms = new MemoryStream(bytes);
			return ReadFrom(ms, privateKey);
		}
		public static SolverServerSession ReadFrom(Stream stream, RsaKey privateKey)
		{
			if(stream == null)
				throw new ArgumentNullException("stream");
			var seria = new SolverSerializer(new SolverParameters(), stream);
			var parameters = seria.ReadParameters();
			seria = new SolverSerializer(parameters, stream);
			var key = seria.ReadBytes(-1, 10 * 1024);
			if(key.Length == 0 && privateKey == null)
				throw new ArgumentException("You should provide a private key");
			privateKey = privateKey ?? new RsaKey(key);
			parameters.ServerKey = privateKey.PubKey;
			SolverServerSession session = new SolverServerSession(privateKey, parameters);
			session._State = (SolverServerStates)seria.ReadUInt();
			var solvedPuzzleCount = (int)seria.ReadUInt();
			session._SolvedPuzzles = new SolvedPuzzle[solvedPuzzleCount];
			for(int i = 0; i < solvedPuzzleCount; i++)
			{
				var v = seria.ReadPuzzle();
				var solutionKey = new SolutionKey(seria.ReadBytes(SolutionKey.KeySize));
				var solution = new PuzzleSolution(seria.ReadBigInteger(seria.GetKeySize()));
				session._SolvedPuzzles[i] = new SolvedPuzzle(new Puzzle(parameters.ServerKey, v), solutionKey, solution);
			}
			return session;
		}
		public void WriteTo(Stream stream, bool includePrivateKey)
		{
			if(stream == null)
				throw new ArgumentNullException("stream");
			var seria = new SolverSerializer(Parameters, stream);
			seria.WriteParameters();
			seria.WriteBytes(includePrivateKey ? ServerKey.ToBytes(): new byte[0], false);
			seria.WriteUInt((uint)_State);
			seria.WriteUInt(_SolvedPuzzles.Length);
			foreach(var solvedPuzzle in _SolvedPuzzles)
			{
				seria.WritePuzzle(solvedPuzzle.Puzzle.PuzzleValue);
				seria.WriteBytes(solvedPuzzle.SolutionKey.ToBytes(true), true);
				seria.WriteBigInteger(solvedPuzzle.Solution._Value, seria.GetKeySize());
			}
		}
		public byte[] ToBytes(bool includePrivateKey)
		{
			MemoryStream ms = new MemoryStream();
			WriteTo(ms, includePrivateKey);
			ms.Position = 0;
			return ms.ToArrayEfficient();
		}

		public SolverParameters Parameters
		{
			get
			{
				return _Parameters;
			}
		}
		
		public SolverServerStates State
		{
			get
			{
				return _State;
			}
		}


		public RsaKey ServerKey
		{
			get
			{
				return _ServerKey;
			}
		}	

		public ServerCommitment[] SolvePuzzles(PuzzleValue[] puzzles)
		{
			if(puzzles == null)
				throw new ArgumentNullException("puzzles");
			if(puzzles.Length != Parameters.GetTotalCount())
				throw new ArgumentException("Expecting " + Parameters.GetTotalCount() + " puzzles");
			AssertState(SolverServerStates.WaitingPuzzles);
			List<ServerCommitment> commitments = new List<ServerCommitment>();
			List<SolvedPuzzle> solvedPuzzles = new List<SolvedPuzzle>();
			foreach(var puzzle in puzzles)
			{
				var solution = puzzle.Solve(ServerKey);
				byte[] key = null;
				var encryptedSolution = Utils.ChachaEncrypt(solution.ToBytes(), ref key);
				var solutionKey = new SolutionKey(key);
				uint160 keyHash = solutionKey.GetHash();
				commitments.Add(new ServerCommitment(keyHash, encryptedSolution));
				solvedPuzzles.Add(new SolvedPuzzle(new Puzzle(ServerKey.PubKey, puzzle), solutionKey, solution));
			}
			_SolvedPuzzles = solvedPuzzles.ToArray();
			_State = SolverServerStates.WaitingRevelation;
			return commitments.ToArray();
		}

		public SolutionKey[] CheckRevelation(ClientRevelation revelation)
		{
			if(revelation == null)
				throw new ArgumentNullException("puzzleSolutions");
			if(revelation.Indexes.Length != Parameters.FakePuzzleCount || revelation.Solutions.Length != Parameters.FakePuzzleCount)
				throw new ArgumentException("Expecting " + Parameters.FakePuzzleCount + " puzzle solutions");
			AssertState(SolverServerStates.WaitingRevelation);



			List<SolvedPuzzle> fakePuzzles = new List<SolvedPuzzle>();
			for(int i = 0; i < Parameters.FakePuzzleCount; i++)
			{
				var index = revelation.Indexes[i];
				var solvedPuzzle = _SolvedPuzzles[index];
				if(solvedPuzzle.Solution != revelation.Solutions[i])
				{
					throw new PuzzleException("Incorrect puzzle solution");
				}
				fakePuzzles.Add(solvedPuzzle);
			}

			List<SolvedPuzzle> realPuzzles = new List<SolvedPuzzle>();
			for(int i = 0; i < Parameters.GetTotalCount(); i++)
			{
				if(Array.IndexOf(revelation.Indexes, i) == -1)
				{
					realPuzzles.Add(_SolvedPuzzles[i]);
				}
			}						
			_SolvedPuzzles = realPuzzles.ToArray();
			_State = SolverServerStates.WaitingBlindFactor;
			return fakePuzzles.Select(f => f.SolutionKey).ToArray();
		}

		public void CheckBlindedFactors(BlindFactor[] blindFactors)
		{
			if(blindFactors == null)
				throw new ArgumentNullException("blindFactors");
			if(blindFactors.Length != Parameters.RealPuzzleCount)
				throw new ArgumentException("Expecting " + Parameters.RealPuzzleCount + " blind factors");
			AssertState(SolverServerStates.WaitingBlindFactor);
			List<SolutionKey> keys = new List<SolutionKey>();
			Puzzle unblindedPuzzle = null;
			int y = 0;
			for(int i = 0; i < Parameters.RealPuzzleCount; i++)
			{
				var solvedPuzzle = _SolvedPuzzles[i];
				var unblinded = solvedPuzzle.Puzzle.Unblind(blindFactors[i]);
				if(unblindedPuzzle == null)
					unblindedPuzzle = unblinded;
				else if(unblinded != unblindedPuzzle)
					throw new PuzzleException("Invalid blind factor");
				y++;
			}
			_State = SolverServerStates.Completed;
		}

		public SolutionKey[] GetSolutionKeys()
		{
			AssertState(SolverServerStates.Completed);
			return _SolvedPuzzles.Select(s => s.SolutionKey).ToArray();
		}

		public Script GetFulfillScript(PaymentCashoutContext escrowContext, TransactionSignature cashoutSignature)
		{
			if(escrowContext == null)
				throw new ArgumentNullException("escrowContext");
			if(cashoutSignature == null)
				throw new ArgumentNullException("cashoutSignature");
			return escrowContext.CreateFulfillScript(escrowContext, cashoutSignature, GetSolutionKeys());
		}

		private void AssertState(SolverServerStates state)
		{
			if(state != _State)
				throw new InvalidOperationException("Invalid state, actual " + _State + " while expected is " + state);
		}		
	}
}
