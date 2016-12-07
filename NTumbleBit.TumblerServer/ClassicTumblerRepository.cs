using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NTumbleBit.ClassicTumbler;
using NBitcoin;

namespace NTumbleBit.TumblerServer
{
	public class ClassicTumblerRepository
	{
		static Dictionary<string, TumblerBobServerSession> _BobSessions = new Dictionary<string, TumblerBobServerSession>();
		static Dictionary<string, TumblerAliceServerSession> _AliceSessions = new Dictionary<string, TumblerAliceServerSession>();

		public void Save(string sessionId, TumblerBobServerSession session)
		{
			_BobSessions.AddOrReplace(sessionId, session);
		}

		public TumblerBobServerSession GetBobSession(string sessionId)
		{
			return _BobSessions.TryGet(sessionId);
		}

		public void Save(string sessionId, TumblerAliceServerSession session)
		{
			_AliceSessions.AddOrReplace(sessionId, session);
		}

		public TumblerAliceServerSession GetAliceSession(string sessionId)
		{
			return _AliceSessions.TryGet(sessionId);
		}
	}
}
