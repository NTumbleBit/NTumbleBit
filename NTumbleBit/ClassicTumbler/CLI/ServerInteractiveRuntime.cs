using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using NBitcoin;
using NTumbleBit.Services;
using NTumbleBit.ClassicTumbler.Server;
using NTumbleBit.ClassicTumbler.Client;

namespace NTumbleBit.ClassicTumbler.CLI
{
	public class ServerInteractiveRuntime : IInteractiveRuntime
	{
		public ServerInteractiveRuntime(TumblerRuntime runtime)
		{
			if(runtime == null)
				throw new ArgumentNullException("runtime");
			InnerRuntime = runtime;
		}
		public TumblerRuntime InnerRuntime
		{
			get;
			private set;
		}
		public ClassicTumblerParameters TumblerParameters => InnerRuntime.ClassicTumblerParameters;

		public Network Network => InnerRuntime.Network;

		public Tracker Tracker => InnerRuntime.Tracker;

		public ExternalServices Services => InnerRuntime.Services;

		public IDestinationWallet DestinationWallet => null;

		public void Dispose()
		{
			InnerRuntime.Dispose();
		}
	}
}
