using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NTumbleBit.ClassicTumbler;
using NBitcoin;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using NTumbleBit.TumblerServer.Services;

namespace NTumbleBit.TumblerServer
{
	public class ClassicTumblerRepository
	{
		public ClassicTumblerRepository(TumblerConfiguration config, IRepository repository)
		{
			if(config == null)
				throw new ArgumentNullException(nameof(config));
			_Configuration = config;
			_Repository = repository;
		}


		private readonly IRepository _Repository;
		public IRepository Repository
		{
			get
			{
				return _Repository;
			}
		}

		private readonly TumblerConfiguration _Configuration;
		public TumblerConfiguration Configuration
		{
			get
			{
				return _Configuration;
			}
		}

		public void Save(int cycleId, PromiseServerSession session)
		{
			Repository.UpdateOrInsert(GetCyclePartition(cycleId), session.Id, session.GetInternalState(), (o, n) =>
			{
				if(o.ETag != n.ETag)
					throw new InvalidOperationException("Optimistic concurrency failure");
				n.ETag++;
				return n;
			});
		}

		public void Save(int cycleId, SolverServerSession session)
		{
			Repository.UpdateOrInsert(GetCyclePartition(cycleId), session.Id, session.GetInternalState(), (o, n) =>
			{
				if(o.ETag != n.ETag)
					throw new InvalidOperationException("Optimistic concurrency failure");
				n.ETag++;
				return n;
			});
		}

		public PromiseServerSession GetPromiseServerSession(int cycleId, string id)
		{
			var session = Repository.Get<PromiseServerSession.State>(GetCyclePartition(cycleId), id);
			if(session == null)
				return null;
			return new PromiseServerSession(session,
				_Configuration.CreateClassicTumblerParameters().CreatePromiseParamaters());
		}

		public SolverServerSession GetSolverServerSession(int cycleId, string id)
		{
			var session = Repository.Get<SolverServerSession.State>(GetCyclePartition(cycleId), id);
			if(session == null)
				return null;
			return new SolverServerSession(_Configuration.TumblerKey,
				_Configuration.CreateClassicTumblerParameters().CreateSolverParamaters(),
				session);
		}

		public Key GetNextKey(int cycleId, out int keyIndex)
		{
			ExtKey key = GetExtKey();
			var partition = GetCyclePartition(cycleId);
			var nextIndex = Repository.Get<int>(partition, "KeyIndex") + 1;
			Repository.UpdateOrInsert<int>(partition, "KeyIndex", nextIndex, (o, n) =>
			{
				nextIndex = Math.Max(o, n);
				return nextIndex;
			});
			keyIndex = nextIndex - 1;
			return key.Derive(cycleId, false).Derive((uint)keyIndex).PrivateKey;
		}

		private ExtKey GetExtKey()
		{
			var key = Repository.Get<ExtKey>("General", "EscrowHDKey");
			if(key == null)
			{
				key = new ExtKey();
				Repository.UpdateOrInsert<ExtKey>("General", "EscrowHDKey", key, (o, n) =>
				{
					key = o;
					return o;
				});
			}
			return key;
		}

		public Key GetKey(int cycleId, int keyIndex)
		{
			return GetExtKey().Derive(cycleId, false).Derive((uint)keyIndex).PrivateKey;
		}

		private static string GetCyclePartition(int cycleId)
		{
			return "Cycle_" + cycleId;
		}

		public bool MarkUsedNonce(int cycle, uint160 nonce)
		{
			bool used = false;
			var partition = GetCyclePartition(cycle);
			Repository.UpdateOrInsert<bool>(partition, "Nonces-" + nonce, true, (o, n) =>
			{
				used = true;
				return n;
			});
			return !used;
		}
	}
}
