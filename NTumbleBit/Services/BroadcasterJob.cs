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
	public class BroadcasterJob : TumblerServiceBase
	{
		public BroadcasterJob(IExternalServices services)
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

		public override string Name => "broadcaster";		

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

		protected override void StartCore(CancellationToken cancellationToken)
		{
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
							lastBlock = BlockExplorerService.WaitBlock(lastBlock, cancellationToken);
							TryBroadcast();
						}
					}
					catch(OperationCanceledException ex)
					{
						if(cancellationToken.IsCancellationRequested)
						{
							Stopped();
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
						cancellationToken.WaitHandle.WaitOne(5000);
					}
				}
			}).Start();
		}
	}
}
