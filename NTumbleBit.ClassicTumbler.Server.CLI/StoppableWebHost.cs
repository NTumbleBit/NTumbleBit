using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NTumbleBit.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace NTumbleBit
{
	public class StoppableWebHost : TumblerServiceBase
	{
		Func<IWebHost> _HostBuilder;
		public StoppableWebHost(Func<IWebHost> builder)
		{
			if(builder == null)
				throw new ArgumentNullException("builder");
			_HostBuilder = builder;
		}
		public override string Name => "tumbler";

		protected override void StartCore(CancellationToken cancellationToken)
		{
			new Thread(() =>
			{
				IWebHost host = null;
				try
				{
					host = _HostBuilder();
					host.Run(cancellationToken);
				}
				catch(Exception ex)
				{
					if(!cancellationToken.IsCancellationRequested)
						Logs.Tumbler.LogCritical(1, ex, "Error while starting the host");
				}
				finally
				{
					try
					{
						if(host != null)
							host.Dispose();
					}
					catch { }
				}
				Stopped();
			}).Start();
		}
	}
}
