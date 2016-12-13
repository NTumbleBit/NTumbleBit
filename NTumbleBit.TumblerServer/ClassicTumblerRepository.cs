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


		public void Save(string sessionId, BobServerChannelNegotiation session)
		{
			Repository.Add("Negotiation", sessionId, session.GetInternalState());
		}

		public void Save(PromiseServerSession session)
		{
			Repository.Add("Sessions", session.Id, session.GetInternalState());
		}

		public void Save(SolverServerSession session)
		{
			Repository.Add("Sessions", session.Id, session.GetInternalState());
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


		public BobServerChannelNegotiation GetBobSession(string sessionId)
		{
			var state = Repository.Get<BobServerChannelNegotiation.State>("Negotiation", sessionId);
			if(state == null)
				return null;
			return new BobServerChannelNegotiation(_Configuration.CreateClassicTumblerParameters(), _Configuration.TumblerKey, _Configuration.VoucherKey, state);
		}

		public void Save(string sessionId, AliceServerChannelNegotiation session)
		{
			Repository.Add("Negotiation", sessionId, session.GetInternalState());
		}

		public AliceServerChannelNegotiation GetAliceSession(string sessionId)
		{
			var state = Repository.Get<AliceServerChannelNegotiation.State>("Negotiation", sessionId);
			if(state == null)
				return null;			
			return new AliceServerChannelNegotiation(_Configuration.CreateClassicTumblerParameters(), _Configuration.TumblerKey, _Configuration.VoucherKey, state);
		}		
	}
}
