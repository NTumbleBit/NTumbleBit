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
			Logs.Configure(new FuncLoggerFactory(i => new ConsoleLogger(i, (a, b) => true, false)));
			var configuration = new TumblerConfiguration();
			configuration.LoadArgs(args);
			try
			{
				IWebHost host = null;
				ExternalServices services = null;
				if(!configuration.OnlyMonitor)
				{
					host = new WebHostBuilder()
					.UseKestrel()
					.UseAppConfiguration(configuration)
					.UseContentRoot(Directory.GetCurrentDirectory())
					.UseIISIntegration()
					.UseStartup<Startup>()
					.Build();
					services = (ExternalServices)host.Services.GetService(typeof(ExternalServices));
				}
				else
				{
					var dbreeze = new DBreezeRepository(Path.Combine(configuration.DataDir, "db"));
					services = ExternalServices.CreateFromRPCClient(configuration.RPC.ConfigureRPCClient(configuration.Network), dbreeze, new Tracker(dbreeze));
				}

				CancellationTokenSource cts = new CancellationTokenSource();
				var job = new BroadcasterJob(services, Logs.Main);
				job.Start(cts.Token);
				Logs.Main.LogInformation("BroadcasterJob started");

				if(!configuration.OnlyMonitor)
					host.Run();
				else
					Console.ReadLine();
				cts.Cancel();
			}
			catch(ConfigException ex)
			{
				if(!string.IsNullOrEmpty(ex.Message))
					Logs.Main.LogError(ex.Message);
			}
			catch(Exception exception)
			{
				Logs.Main.LogError("Exception thrown while running the server " + exception.Message);
			}
		}
	}
}

