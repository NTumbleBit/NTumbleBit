using Microsoft.Extensions.Logging;
using NBitcoin;
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
					Exception unhandled = null;
					try
					{
						uint256 lastBlock = uint256.Zero;
						while(true)
						{
							lastBlock = BlockExplorerService.WaitBlock(lastBlock, _Stop);
							var height = BlockExplorerService.GetCurrentHeight();
							uint256[] knownBroadcasted = null;
							try
							{
								var transactions = BroadcasterService.TryBroadcast(ref knownBroadcasted);
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
								var transactions = TrustedBroadcasterService.TryBroadcast(ref knownBroadcasted);
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
					catch(OperationCanceledException ex)
					{
						if(_Stop.IsCancellationRequested)
						{
							Logger.LogInformation("BroadcasterJob stopped");
							break;
						}
						else
							unhandled = ex;
					}
					catch(Exception ex)
					{
						unhandled = ex;
					}
					if(unhandled != null)
					{
						Logger.LogError("Uncaught exception BroadcasterJob : " + unhandled.ToString());
						_Stop.WaitHandle.WaitOne(5000);
					}
				}
			}).Start();
		}
	}
}
