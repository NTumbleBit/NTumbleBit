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
				throw new ArgumentNullException("config");
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

		public void Save(PromiseServerSession session)
		{
			Repository.UpdateOrInsert("Sessions", session.Id, session.GetInternalState(), (o, n) => n);
		}

		public void Save(SolverServerSession session)
		{
			Repository.UpdateOrInsert("Sessions", session.Id, session.GetInternalState(), (o, n) => n);
		}

		public PromiseServerSession GetPromiseServerSession(string id)
		{
			var session = Repository.Get<PromiseServerSession.State>("Sessions", id);
			if(session == null)
				return null;
			return new PromiseServerSession(session,
				_Configuration.CreateClassicTumblerParameters().CreatePromiseParamaters());
		}

		public SolverServerSession GetSolverServerSession(string id)
		{
			var session = Repository.Get<SolverServerSession.State>("Sessions", id);
			if(session == null)
				return null;
			return new SolverServerSession(_Configuration.TumblerKey,
				this._Configuration.CreateClassicTumblerParameters().CreateSolverParamaters(),
				session);
		}

		public void Save(string sessionId, AliceServerChannelNegotiation session)
		{
			Repository.UpdateOrInsert("Negotiation", sessionId, session.GetInternalState(), (o, n) => n);
		}

		public AliceServerChannelNegotiation GetAliceSession(string sessionId)
		{
			var state = Repository.Get<AliceServerChannelNegotiation.State>("Negotiation", sessionId);
			if(state == null)
				return null;
			return new AliceServerChannelNegotiation(_Configuration.CreateClassicTumblerParameters(), _Configuration.TumblerKey, _Configuration.VoucherKey, state);
		}

		public bool MarkUsedNonce(int cycle, uint160 nonce)
		{
			bool used = false;
			Repository.UpdateOrInsert("Nonces", cycle.ToString(), nonce, (o, n) =>
			{
				used = true;
				return n;
			});
			return !used;
		}
	}
}
