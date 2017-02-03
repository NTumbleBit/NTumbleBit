using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NBitcoin;
using NTumbleBit.Common;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging;
using NTumbleBit.TumblerServer.Services;
using System.Threading;
using NTumbleBit.Common.Logging;

namespace NTumbleBit.TumblerServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
			Logs.Configure(new FuncLoggerFactory(i => new ConsoleLogger("Configuration", (a, b) => true, false)));
			Logs.Main.LogInformation("Network " + BC2.Network);
			var logger = new ConsoleLogger("Main", (a, b) => true, false);
			var configuration = new TumblerConfiguration();
			configuration.LoadArgs(args);
			try
			{
				var host = new WebHostBuilder()
				.UseKestrel()
				.UseAppConfiguration(configuration)
				.UseContentRoot(Directory.GetCurrentDirectory())
				.UseIISIntegration()
				.UseStartup<Startup>()
				.Build();

				var services = (ExternalServices)host.Services.GetService(typeof(ExternalServices));
				CancellationTokenSource cts = new CancellationTokenSource();
				var job = new BroadcasterJob(services, logger);
				job.Start(cts.Token);
				host.Run();
				cts.Cancel();
			}
			catch(ConfigException ex)
			{
				if(!string.IsNullOrEmpty(ex.Message))
					logger.LogError(ex.Message);
			}
			catch(Exception exception)
			{
				logger.LogError("Exception thrown while running the server " + exception.Message);
			}
		}
	}
}

