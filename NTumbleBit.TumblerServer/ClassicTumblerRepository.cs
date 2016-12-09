using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NTumbleBit.ClassicTumbler;
using NBitcoin;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;

namespace NTumbleBit.TumblerServer
{
	public class ClassicTumblerRepository
	{
		public ClassicTumblerRepository(TumblerConfiguration config)
		{
			if(config == null)
				throw new ArgumentNullException("config");
			_Configuration = config;
		}


		private readonly TumblerConfiguration _Configuration;
		public TumblerConfiguration Configuration
		{
			get
			{
				return _Configuration;
			}
		}

		static Dictionary<string, string> _BobSessions = new Dictionary<string, string>();
		static Dictionary<string, string> _AliceSessions = new Dictionary<string, string>();
		static Dictionary<string, string> _Promises = new Dictionary<string, string>();

		public void Save(string sessionId, TumblerBobServerSession session)
		{
			_BobSessions.AddOrReplace(sessionId, Serializer.ToString(session.GetInternalState()));
		}

		public void Save(PromiseServerSession session)
		{
			_Promises.AddOrReplace(session.Id, Serializer.ToString(session.GetInternalState()));
		}

		public void Save(SolverServerSession session)
		{
			_Promises.AddOrReplace(session.Id, Serializer.ToString(session.GetInternalState()));
		}

		public PromiseServerSession GetPromiseServerSession(string id)
		{
			var session = _Promises.TryGet(id);
			if(session == null)
				return null;
			return new PromiseServerSession(Serializer.ToObject<PromiseServerSession.InternalState>(session),
				_Configuration.CreateClassicTumblerParameters().CreatePromiseParamaters());
		}

		public SolverServerSession GetSolverServerSession(string id)
		{
			var session = _Promises.TryGet(id);
			if(session == null)
				return null;
			return new SolverServerSession(_Configuration.TumblerKey,
				this._Configuration.CreateClassicTumblerParameters().CreateSolverParamaters(),
				Serializer.ToObject<SolverServerSession.InternalState>(session));
		}


		public TumblerBobServerSession GetBobSession(string sessionId)
		{
			var data = _BobSessions.TryGet(sessionId);
			if(data == null)
				return null;

			TumblerBobServerSession.State state = Serializer.ToObject<TumblerBobServerSession.State>(data);
			return new TumblerBobServerSession(_Configuration.CreateClassicTumblerParameters(), _Configuration.TumblerKey, _Configuration.VoucherKey, state);
		}

		public void Save(string sessionId, TumblerAliceServerSession session)
		{
			_AliceSessions.AddOrReplace(sessionId, Serializer.ToString(session.GetInternalState()));
		}

		public TumblerAliceServerSession GetAliceSession(string sessionId)
		{
			var data = _AliceSessions.TryGet(sessionId);
			if(data == null)
				return null;

			TumblerAliceServerSession.State state = Serializer.ToObject<TumblerAliceServerSession.State>(data);
			return new TumblerAliceServerSession(_Configuration.CreateClassicTumblerParameters(), _Configuration.TumblerKey, _Configuration.VoucherKey, state);
		}		
	}
}
