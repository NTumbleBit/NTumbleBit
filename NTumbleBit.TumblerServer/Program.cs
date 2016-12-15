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

namespace NTumbleBit.TumblerServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
			var logger = new ConsoleLogger("Main", (a, b) => true, false);
			var configuration = new TumblerConfiguration();
			if(args.Contains("-testnet"))
			{
				configuration.Network = Network.TestNet;
				var cycle = configuration
					.ClassicTumblerParameters
					.CycleGenerator.FirstCycle;

				cycle.RegistrationDuration = 3;
				cycle.Start = 0;
				cycle.RegistrationDuration = 3;
				cycle.ClientChannelEstablishmentDuration = 3;
				cycle.TumblerChannelEstablishmentDuration = 3;
				cycle.SafetyPeriodDuration = 2;
				cycle.PaymentPhaseDuration = 3;
				cycle.TumblerCashoutDuration = 4;
				cycle.ClientCashoutDuration = 3;
			}
			
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
