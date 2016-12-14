using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NBitcoin;

namespace NTumbleBit.TumblerServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
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
			catch(Exception exception)
			{
				Console.WriteLine("Exception thrown while running the server " + exception.Message);
			}
		}
	}
}
