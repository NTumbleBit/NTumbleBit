using NTumbleBit.ClassicTumbler.Client;
using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using NTumbleBit.Services;
using System.Threading;

namespace NTumbleBit.ClassicTumbler.CLI
{
	public class ClientInteractiveRuntime : IInteractiveRuntime
    {
		public ClientInteractiveRuntime(TumblerClientRuntime runtime)
		{
            InnerRuntime = runtime ?? throw new ArgumentNullException("runtime");
		}


		public TumblerClientRuntime InnerRuntime
		{
			get; set;
		}

		public ClassicTumblerParameters TumblerParameters => InnerRuntime.TumblerParameters;

		public Network Network => InnerRuntime.Network;

		public Tracker Tracker => InnerRuntime.Tracker;

		public ExternalServices Services => InnerRuntime.Services;

		public IDestinationWallet DestinationWallet => InnerRuntime.DestinationWallet;

		public void Dispose()
		{
			InnerRuntime.Dispose();
		}
	}
}
