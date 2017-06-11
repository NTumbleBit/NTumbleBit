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
using NTumbleBit.ClassicTumbler;

namespace NTumbleBit.TumblerServer
{
	public partial class Program
	{
		public ClassicTumblerParameters TumblerParameters
		{
			get; set;
		}
		public Network Network
		{
			get; set;
		}
		public Tracker Tracker
		{
			get; set;
		}
		public ExternalServices Services
		{
			get; set;
		}
		public static void Main(string[] args)
		{
			new Program().Run(args);
		}
		public void Run(string[] args)
		{
			Logs.Configure(new FuncLoggerFactory(i => new ConsoleLogger(i, (a, b) => true, false)));
			var config = new TumblerConfiguration();
			config.LoadArgs(args);
			try
			{
				IWebHost host = null;
				if(!config.OnlyMonitor)
				{
					host = new WebHostBuilder()
					.UseKestrel()
					.UseAppConfiguration(config)
					.UseContentRoot(Directory.GetCurrentDirectory())
					.UseIISIntegration()
					.UseStartup<Startup>()
					.Build();
					Services = (ExternalServices)host.Services.GetService(typeof(ExternalServices));
				}
				else
				{
					var dbreeze = new DBreezeRepository(Path.Combine(config.DataDir, "db"));
					Tracker = new Tracker(dbreeze);
					Services = ExternalServices.CreateFromRPCClient(config.RPC.ConfigureRPCClient(TumblerParameters.Network), dbreeze, Tracker);
				}

				CancellationTokenSource cts = new CancellationTokenSource();
				var job = new BroadcasterJob(Services, Logs.Main);
				job.Start(cts.Token);
				Logs.Main.LogInformation("BroadcasterJob started");

				TumblerParameters = config.ClassicTumblerParameters;
				Network = config.Network;

				if(!config.OnlyMonitor)
					new Thread(() =>
					{
						try
						{
							host.Run(cts.Token);
						}
						catch(Exception ex)
						{
							Logs.Server.LogCritical(1, ex, "Error while starting the host");
						}
					}).Start();
				StartInteractive();
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

