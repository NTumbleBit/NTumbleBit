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

		public void Save(string sessionId, BobServerChannelNegotiation session)
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
			return new PromiseServerSession(Serializer.ToObject<PromiseServerSession.State>(session),
				_Configuration.CreateClassicTumblerParameters().CreatePromiseParamaters());
		}

		public SolverServerSession GetSolverServerSession(string id)
		{
			var session = _Promises.TryGet(id);
			if(session == null)
				return null;
			return new SolverServerSession(_Configuration.TumblerKey,
				this._Configuration.CreateClassicTumblerParameters().CreateSolverParamaters(),
				Serializer.ToObject<SolverServerSession.State>(session));
		}


		public BobServerChannelNegotiation GetBobSession(string sessionId)
		{
			var data = _BobSessions.TryGet(sessionId);
			if(data == null)
				return null;

			BobServerChannelNegotiation.State state = Serializer.ToObject<BobServerChannelNegotiation.State>(data);
			return new BobServerChannelNegotiation(_Configuration.CreateClassicTumblerParameters(), _Configuration.TumblerKey, _Configuration.VoucherKey, state);
		}

		public void Save(string sessionId, AliceServerChannelNegotiation session)
		{
			_AliceSessions.AddOrReplace(sessionId, Serializer.ToString(session.GetInternalState()));
		}

		public AliceServerChannelNegotiation GetAliceSession(string sessionId)
		{
			var data = _AliceSessions.TryGet(sessionId);
			if(data == null)
				return null;

			AliceServerChannelNegotiation.State state = Serializer.ToObject<AliceServerChannelNegotiation.State>(data);
			return new AliceServerChannelNegotiation(_Configuration.CreateClassicTumblerParameters(), _Configuration.TumblerKey, _Configuration.VoucherKey, state);
		}		
	}
}
