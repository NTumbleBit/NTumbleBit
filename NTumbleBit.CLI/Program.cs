using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using NTumbleBit.Logging;
using System.Text;
using NBitcoin.RPC;
using CommandLine;
using System.Reflection;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.Configuration;
using NTumbleBit.ClassicTumbler.Client;
using NTumbleBit.ClassicTumbler.CLI;

namespace NTumbleBit.ClassicTumbler.Client.CLI
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

			using(var interactive = new Interactive())
			{

				try
				{
					var config = new TumblerClientConfiguration();
					config.LoadArgs(args);

					ClassicTumblerParameters toConfirm;
					var runtime = TumblerClientRuntime.FromConfiguration(config, out toConfirm);
					if(toConfirm != null)
					{
						if(!PromptConfirmation(toConfirm))
						{
							Logs.Main.LogInformation("New tumbler parameters refused");
							return;
						}
						runtime.Confirm(toConfirm);
					}

					interactive.Runtime = new ClientInteractiveRuntime(runtime);
					

					var broadcaster = runtime.CreateBroadcasterJob();
					broadcaster.Start(interactive.BroadcasterCancellationToken);
					Logs.Main.LogInformation("BroadcasterJob started");

					if(!config.OnlyMonitor)
					{
						var client = new TumblerClient(runtime.Network, config.TumblerServer);
						var stateMachine = runtime.CreateStateMachineJob();
						stateMachine.Start(interactive.MixingCancellationToken);
						Logs.Main.LogInformation("State machines started");
					}


					interactive.StartInteractive();
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
			Console.WriteLine("Do you confirm the following tumbler settings? (type 'yes' to accept)");
			var response = Console.ReadLine();
			return response.Equals("yes", StringComparison.OrdinalIgnoreCase);
		}
	}
}
