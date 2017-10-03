using NTumbleBit.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using NTumbleBit.ClassicTumbler.Server;
using System.Threading.Tasks;
using NTumbleBit.Tor;

namespace NTumbleBit.Services
{
	public class TorRegisterJob : TumblerServiceBase
	{
		public override string Name => "torregister";
		TumblerRuntime runtime;
		TumblerConfiguration conf;
		public TorRegisterJob(TumblerConfiguration conf, TumblerRuntime runtime)
		{
			this.conf = conf;
			this.runtime = runtime;
		}

		protected override void StartCore(CancellationToken cancellationToken)
		{
			new Thread(() =>
			{
				Logs.Tumbler.LogInformation("Tor Register job started");
				while(true)
				{
					Exception unhandled = null;
					try
					{
						cancellationToken.ThrowIfCancellationRequested();
						RunTask().GetAwaiter().GetResult();
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
						Logs.Tumbler.LogError("Uncaught exception Tor Register job : " + unhandled.ToString());
					}
					cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMinutes(5));
				}
			})
			{
				Name = Name
			}.Start();
		}

		private async Task RunTask()
		{
			if(conf.TorSettings == null || runtime.TorPrivateKey == null)
				return;
			await runtime.TorConnection.ConnectAsync().ConfigureAwait(false);
			await runtime.TorConnection.AuthenticateAsync().ConfigureAwait(false);
			try
			{
				await runtime.TorConnection.RegisterHiddenServiceAsync(conf.Listen, conf.TorSettings.VirtualPort, runtime.TorPrivateKey).ConfigureAwait(false);
				Logs.Tumbler.LogDebug("Tor hidden service registration renewed");
			}
			catch(TorException ex) when(ex.TorResponse.StartsWith("550"))
			{
				Logs.Tumbler.LogDebug("Tor hidden service already registered");
			}
		}
	}
}
