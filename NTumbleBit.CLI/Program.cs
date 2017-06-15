using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NTumbleBit.Common;
using System.IO;
using NTumbleBit.Client.Tumbler.Services;
using NTumbleBit.Client.Tumbler;
using System.Threading;
using NTumbleBit.Common.Logging;
using System.Text;
using NBitcoin.RPC;
using CommandLine;
using System.Reflection;
using NTumbleBit.ClassicTumbler;

namespace NTumbleBit.CLI
{
	public partial class Program
	{
		public static void Main(string[] args)
		{
			new Program().Run(args);
		}
		public void Run(string[] args)
		{
			Logs.Configure(new FuncLoggerFactory(i => new ConsoleLogger(i, (a, b) => true, false)));
			BroadcasterToken = new CancellationTokenSource();
			MixingToken = new CancellationTokenSource();
			DBreezeRepository dbreeze = null;
			try
			{
				var config = new TumblerClientConfiguration();
				config.LoadArgs(args);
				Network = config.Network;

				ClassicTumblerParameters toConfirm;
				var runtime = TumblerClientRuntime.FromConfiguration(config, out toConfirm);
				if(toConfirm != null)
				{
					if(!PromptConfirmation(toConfirm))
					{
						runtime.Confirm(toConfirm);
						Logs.Main.LogInformation("New tumbler parameters refused");
						return;
					}
				}

				Tracker = runtime.Tracker;
				Services = runtime.Services;

				var broadcaster = runtime.CreateBroadcasterJob();
				broadcaster.Start(BroadcasterToken.Token);
				Logs.Main.LogInformation("BroadcasterJob started");

				if(!config.OnlyMonitor)
				{
					var client = new TumblerClient(Network, config.TumblerServer);
					TumblerParameters = runtime.TumblerParameters;
					var stateMachine = runtime.CreateStateMachineJob();
					stateMachine.Start(MixingToken.Token);
					Logs.Main.LogInformation("State machines started");
				}


				StartInteractive();
			}
			catch(ConfigException ex)
			{
				if(!string.IsNullOrEmpty(ex.Message))
					Logs.Configuration.LogError(ex.Message);
			}
			catch(Exception ex)
			{
				Logs.Configuration.LogError(ex.Message);
				Logs.Configuration.LogDebug(ex.StackTrace);
			}
			finally
			{
				if(!MixingToken.IsCancellationRequested)
					MixingToken.Cancel();
				if(!BroadcasterToken.IsCancellationRequested)
					BroadcasterToken.Cancel();
				dbreeze?.Dispose();
			}
		}

		private bool PromptConfirmation(ClassicTumblerParameters toConfirm)
		{
			Console.WriteLine("Do you confirm the following tumbler settings? (type 'yes' to accept)");
			Console.WriteLine("------");
			Console.WriteLine(Serializer.ToString(toConfirm));
			Console.WriteLine("--");
			Console.WriteLine("Tumbler Fee: " + toConfirm.Fee.ToString());
			Console.WriteLine("Denomination: " + toConfirm.Denomination.ToString());
			var periods = toConfirm.CycleGenerator.FirstCycle.GetPeriods();
			Console.WriteLine("Total cycle length: " + (periods.Total.End - periods.Total.Start) + " blocks");
			Console.WriteLine("------");
			var response = Console.ReadLine();
			return response.Equals("yes", StringComparison.OrdinalIgnoreCase);
		}
	}
}
