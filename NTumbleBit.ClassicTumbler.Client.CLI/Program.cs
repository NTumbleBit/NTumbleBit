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
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using System.Threading.Tasks;

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
			Logs.Configure(new FuncLoggerFactory(i => new CustomerConsoleLogger(i, (a, b) => true, false)));

			using(var interactive = new Interactive())
			{
				Tor.TorProcess = null;
				try
				{
					var config = new TumblerClientConfiguration();
					config.LoadArgs(args);


					#region TorSetup
					// If the server is on the same machine TOR would refuse the connection, in this case go without Tor
					if (config.TumblerServer.DnsSafeHost == "10.0.2.2" || // VM host
						config.TumblerServer.DnsSafeHost.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
						config.TumblerServer.DnsSafeHost == "127.0.0.1" || // localhost
						config.TumblerServer.DnsSafeHost == "0.0.0.0") // localhost
					{
						Tor.UseTor = false;
						Logs.Configuration.LogInformation("Did not start Tor. Reason: The tumbler server is running on the same machine.");
					}
					else
					{
						Tor.UseTor = true;

						var torProcessStartInfo = new ProcessStartInfo("tor")
						{
							Arguments = Tor.TorArguments,
							UseShellExecute = false,
							CreateNoWindow = true,
							RedirectStandardOutput = true
						};

						try
						{
							// if doesn't fail tor is already running with the control port
							Tor.ControlPortClient.IsCircuitEstabilishedAsync().Wait();
							Logs.Configuration.LogInformation($"Tor is already running, using the existing instance.");
						}
						catch
						{
							Logs.Configuration.LogInformation($"Starting Tor with arguments: {Tor.TorArguments}");
							try
							{
								Tor.TorProcess = Process.Start(torProcessStartInfo);
							}
							catch (Exception ex) when (ex is FileNotFoundException || ex is Win32Exception)
							{
								// https://msdn.microsoft.com/en-us/library/0w4h05yb(v=vs.110).aspx
								// According to MSDN it should be FileNotFoundException, but in reality it throws Win32Exception
								throw new ConfigException("Couldn't start Tor process. Make sure Tor is in your PATH.");
							}
						}
						Logs.Configuration.LogInformation($"Estabilishing Tor circuit.");
						#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
						Tor.MakeSureCircuitEstabilishedAsync();
						while(Tor.State != Tor.TorState.CircuitEstabilished)
						{
							Task.Delay(100).Wait();
						}
						#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
						Logs.Configuration.LogInformation($"Tor circuit is estabilished.");
					}
					#endregion

					ClassicTumblerParameters toConfirm;
					var runtime = TumblerClientRuntime.FromConfiguration(config, out toConfirm);
					if(toConfirm != null)
					{
						if(!PromptConfirmation(toConfirm))
						{
							Logs.Configuration.LogInformation("New tumbler parameters refused");
							return;
						}
						runtime.Confirm(toConfirm);
					}

					interactive.Runtime = new ClientInteractiveRuntime(runtime);
					

					var broadcaster = runtime.CreateBroadcasterJob();
					broadcaster.Start(interactive.BroadcasterCancellationToken);

					if(!config.OnlyMonitor)
					{
						var client = new TumblerClient(runtime.Network, config.TumblerServer);
						var stateMachine = runtime.CreateStateMachineJob();
						stateMachine.Start(interactive.MixingCancellationToken);
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
				finally
				{
					if (Tor.UseTor)
					{
						Tor.Kill();
					}
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
