using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NBitcoin;
using NTumbleBit.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NTumbleBit.Services
{
	public class BroadcasterJob
	{
		public BroadcasterJob(ExternalServices services)
		{
			BroadcasterService = services.BroadcastService;
			TrustedBroadcasterService = services.TrustedBroadcastService;
			BlockExplorerService = services.BlockExplorerService;
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

		private CancellationToken _Stop;
		public void Start(CancellationToken cancellation)
		{
			_Stop = cancellation;
			new Thread(() =>
			{
				Logs.Broadcasters.LogInformation("BroadcasterJob started");
				while(true)
				{
					Exception unhandled = null;
					try
					{
						uint256 lastBlock = uint256.Zero;
						while(true)
						{
							lastBlock = BlockExplorerService.WaitBlock(lastBlock, _Stop);
							TryBroadcast();
						}
					}
					catch(OperationCanceledException ex)
					{
						if(_Stop.IsCancellationRequested)
						{
							Logs.Broadcasters.LogInformation("BroadcasterJob stopped");
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
						Logs.Broadcasters.LogError("Uncaught exception BroadcasterJob : " + unhandled.ToString());
						_Stop.WaitHandle.WaitOne(5000);
					}
				}
			}).Start();
		}

		public Transaction[] TryBroadcast()
		{
			uint256[] knownBroadcasted = null;
			List<Transaction> broadcasted = new List<Transaction>();
			try
			{
				broadcasted.AddRange(BroadcasterService.TryBroadcast(ref knownBroadcasted));
			}
			catch(Exception ex)
			{
				Logs.Broadcasters.LogError("Exception on Broadcaster");
				Logs.Broadcasters.LogError(ex.ToString());
			}
			try
			{
				broadcasted.AddRange(TrustedBroadcasterService.TryBroadcast(ref knownBroadcasted));
			}
			catch(Exception ex)
			{
				Logs.Broadcasters.LogError("Exception on TrustedBroadcaster");
				Logs.Broadcasters.LogError(ex.ToString());
			}
			return broadcasted.ToArray();
		}
	}
}
