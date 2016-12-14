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
				host.Run();
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
