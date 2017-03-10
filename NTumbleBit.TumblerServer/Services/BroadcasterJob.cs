using Microsoft.Extensions.Logging;
using NTumbleBit.Common;
using NTumbleBit.Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#if !CLIENT
namespace NTumbleBit.TumblerServer.Services
#else
namespace NTumbleBit.Client.Tumbler.Services
#endif
{
	public class BroadcasterJob
	{
		public BroadcasterJob(ExternalServices services, ILogger logger = null)
		{
			BroadcasterService = services.BroadcastService;
			TrustedBroadcasterService = services.TrustedBroadcastService;
			BlockExplorerService = services.BlockExplorerService;
			Logger = logger ?? new NullLogger();
		}

		public IBroadcastService BroadcasterService
		{
			get;
			private set;
		}
		public ITrustedBroadcastService TrustedBroadcasterService
		{
			get;
			private set;
		}

		public IBlockExplorerService BlockExplorerService
		{
			get;
			private set;
		}

		public ILogger Logger
		{
			get; set;
		}

		private CancellationToken _Stop;
		public void Start(CancellationToken cancellation)
		{
			_Stop = cancellation;
			new Thread(() =>
			{
				while(true)
				{

					try
					{
						int lastHeight = 0;
						while(true)
						{
							_Stop.WaitHandle.WaitOne(5000);
							_Stop.ThrowIfCancellationRequested();

							var height = BlockExplorerService.GetCurrentHeight();
							if(height == lastHeight)
								continue;
							lastHeight = height;
							try
							{
								var transactions = BroadcasterService.TryBroadcast();
								foreach(var tx in transactions)
								{
									Logger.LogInformation("Broadcaster broadcasted  " + tx.GetHash());
								}
							}
							catch(Exception ex)
							{
								Logger.LogError("Error while running Broadcaster: " + ex.ToString());
							}
							try
							{
								var transactions = TrustedBroadcasterService.TryBroadcast();
								foreach(var tx in transactions)
								{
									Logger.LogInformation("TrustedBroadcaster broadcasted " + tx.GetHash());
								}
							}
							catch(Exception ex)
							{
								Logger.LogError("Error while running TrustedBroadcaster: " + ex.ToString());
							}
						}
					}
					catch(OperationCanceledException) { return; }
					catch(Exception ex)
					{
						Logger.LogError("Uncatched exception in BroadcasterJob: " + ex.ToString());
					}
				}
			}).Start();
		}
	}
}
