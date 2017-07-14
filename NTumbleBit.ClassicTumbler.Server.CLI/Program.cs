using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NBitcoin;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging;
using NTumbleBit.Services;
using System.Threading;
using NTumbleBit.Logging;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.Configuration;
using NTumbleBit.ClassicTumbler.Server;
using NTumbleBit.ClassicTumbler.CLI;

namespace NTumbleBit.ClassicTumbler.Server.CLI
{
	public partial class Program
	{
		public static void Main(string[] args)
		{
			new Program().Run(args);
		}
		public void Run(string[] args)
		{
			Logs.Configure(new FuncLoggerFactory(i => new CustomerConsoleLogger(i, (a, b) => true, false)));

			using(var interactive = new Interactive())
			{
				var config = new TumblerConfiguration();
				config.LoadArgs(args);
				try
				{
					var runtime = TumblerRuntime.FromConfiguration(config, new TextWriterClientInteraction(Console.Out, Console.In));
					interactive.Runtime = new ServerInteractiveRuntime(runtime);
					IWebHost host = null;
					if(!config.OnlyMonitor)
					{
						host = new WebHostBuilder()
						.UseKestrel()
						.UseAppConfiguration(runtime)
						.UseContentRoot(Directory.GetCurrentDirectory())
						.UseStartup<Startup>()
						.UseUrls(config.GetUrls())
						.Build();
					}

					var job = new BroadcasterJob(interactive.Runtime.Services);
					job.Start(interactive.BroadcasterCancellationToken);

					if(!config.OnlyMonitor)
						new Thread(() =>
						{
							try
							{
								host.Run(interactive.MixingCancellationToken);
							}
							catch(Exception ex)
							{
								if(!interactive.MixingCancellationToken.IsCancellationRequested)
									Logs.Tumbler.LogCritical(1, ex, "Error while starting the host");
							}
							if(interactive.MixingCancellationToken.IsCancellationRequested)
								Logs.Tumbler.LogInformation("Server stopped");
						}).Start();
					interactive.StartInteractive();
				}
				catch(ConfigException ex)
				{
					if(!string.IsNullOrEmpty(ex.Message))
						Logs.Configuration.LogError(ex.Message);
				}
				catch(InterruptedConsoleException) { }
				catch(Exception exception)
				{
					Logs.Tumbler.LogError("Exception thrown while running the server");
					Logs.Tumbler.LogError(exception.ToString());
				}
			}
		}
	}
}

