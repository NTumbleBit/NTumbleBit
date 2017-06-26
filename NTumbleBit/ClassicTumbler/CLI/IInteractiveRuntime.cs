using NBitcoin;
using NTumbleBit.ClassicTumbler.Client;
using NTumbleBit.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace NTumbleBit.ClassicTumbler.CLI
{
    public interface IInteractiveRuntime : IDisposable
    {
		ClassicTumblerParameters TumblerParameters
		{
			get;
		}
		Network Network
		{
			get;
		}
		Tracker Tracker
		{
			get;
		}
		ExternalServices Services
		{
			get;
		}
		IDestinationWallet DestinationWallet
		{
			get;
		}
	}
}
