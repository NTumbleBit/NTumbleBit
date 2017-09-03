using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NTumbleBit.ClassicTumbler;
using NBitcoin;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using NTumbleBit.Services;

namespace NTumbleBit.ClassicTumbler.Server
{
	public class ClassicTumblerRepository
	{
		public ClassicTumblerRepository(TumblerRuntime runtime)
		{
			if(runtime == null)
				throw new ArgumentNullException(nameof(runtime));
			_Runtime = runtime;
		}


		IRepository Repository
		{
			get
			{
				return _Runtime.Repository;
			}
		}

		private readonly TumblerRuntime _Runtime;
		public TumblerRuntime Runtime
		{
			get
			{
				return _Runtime;
			}
		}

		public void Save(int cycleId, PromiseServerSession session)
		{
			Repository.UpdateOrInsert(GetCyclePartition(cycleId), session.Id.ToString(), session.GetInternalState(), (o, n) =>
			{
				if(o.ETag != n.ETag)
					throw new InvalidOperationException("Optimistic concurrency failure");
				n.ETag++;
				return n;
			});
		}

		public void Save(int cycleId, SolverServerSession session)
		{
			Repository.UpdateOrInsert(GetCyclePartition(cycleId), session.Id.ToString(), session.GetInternalState(), (o, n) =>
			{
				if(o.ETag != n.ETag)
					throw new InvalidOperationException("Optimistic concurrency failure");
				n.ETag++;
				return n;
			});
		}

		public PromiseServerSession GetPromiseServerSession(int cycleId, uint160 channelId)
		{
			var session = Repository.Get<PromiseServerSession.State>(GetCyclePartition(cycleId), channelId.ToString());
			if(session == null)
				return null;
			return new PromiseServerSession(session,
				_Runtime.ClassicTumblerParameters.CreatePromiseParamaters());
		}

		public SolverServerSession GetSolverServerSession(int cycleId, uint160 channelId)
		{
			var session = Repository.Get<SolverServerSession.State>(GetCyclePartition(cycleId), channelId.ToString());
			if(session == null)
				return null;
			return new SolverServerSession(_Runtime.TumblerKey,
				_Runtime.ClassicTumblerParameters.CreateSolverParamaters(),
				session);
		}

		public Key GetNextKey(int cycleId, out int keyIndex)
		{
			ExtKey key = GetExtKey();
			var partition = GetCyclePartition(cycleId);
			var nextIndex = 0;
			Repository.UpdateOrInsert<int>(partition, "KeyIndex", 0, (o, n) =>
			{
				nextIndex = Math.Max(o, n) + 1;
				return nextIndex;
			});
			keyIndex = nextIndex;
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

		public bool IsUsed(int cycle, uint160 nonce)
		{
			var partition = GetCyclePartition(cycle);
			return Repository.Get<bool>(partition, "Nonces-" + nonce);
		}
	}
}
