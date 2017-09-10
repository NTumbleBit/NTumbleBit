using Microsoft.Extensions.Logging;
using System.Linq;
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
using System.Threading.Tasks;
using NTumbleBit.Services.RPC;

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
			var argsConf = new TextFileConfiguration(args);
			var debug = argsConf.GetOrDefault<bool>("debug", false);
			var redeemEscrows = argsConf.GetOrDefault<bool>("redeemescrows", false); ;

			ConsoleLoggerProcessor loggerProcessor = new ConsoleLoggerProcessor();
			Logs.Configure(new FuncLoggerFactory(i => new CustomerConsoleLogger(i, Logs.SupportDebug(debug), false, loggerProcessor)));
			using(var interactive = new Interactive())
			{
				try
				{
					var config = new TumblerClientConfiguration();
					config.LoadArgs(args);

					var runtime = TumblerClientRuntime.FromConfiguration(config, new TextWriterClientInteraction(Console.Out, Console.In));
					interactive.Runtime = new ClientInteractiveRuntime(runtime);

					if(redeemEscrows)
					{
						RedeemEscrows(runtime).GetAwaiter().GetResult();
					}


					var broadcaster = runtime.CreateBroadcasterJob();
					broadcaster.Start();
					interactive.Services.Add(broadcaster);
					//interactive.Services.Add(new CheckIpService(runtime));
					//interactive.Services.Last().Start();

					if(!config.OnlyMonitor)
					{
						var stateMachine = runtime.CreateStateMachineJob();
						stateMachine.Start();
						interactive.Services.Add(stateMachine);
					}

					interactive.StartInteractive();
				}
				catch(ClientInteractionException ex)
				{
					if(!string.IsNullOrEmpty(ex.Message))
						Logs.Configuration.LogError(ex.Message);
				}
				catch(ConfigException ex)
				{
					if(!string.IsNullOrEmpty(ex.Message))
						Logs.Configuration.LogError(ex.Message);
				}
				catch(InterruptedConsoleException) { }
				catch(Exception ex)
				{
					Logs.Configuration.LogError(ex.Message);
					Logs.Configuration.LogDebug(ex.StackTrace);
				}
			}
		}
		public async Task RedeemEscrows(TumblerClientRuntime runtime)
		{
			Logs.Client.LogInformation("Rescanning redeems");
			var rpc = ((RPCBlockExplorerService)runtime.Services.BlockExplorerService).RPCClient;
			var rate = await runtime.Services.FeeService.GetFeeRateAsync();
			foreach(var unspent in await rpc.ListUnspentAsync(0, 99999999))
			{
				foreach(var record in runtime.Tracker.Search(unspent.ScriptPubKey))
				{
					if(record.TransactionType == Services.TransactionType.ClientEscape)
					{
						Logs.Client.LogInformation("Client Escrow found " + record.TransactionId);
						var cycle = runtime.TumblerParameters.CycleGenerator.GetCycle(record.Cycle);
						var machineState = runtime.CreateStateMachineJob().GetPaymentStateMachineState(cycle);
						var solver = new PaymentStateMachine(runtime, machineState).SolverClientSession;
						var broadcast = solver.CreateRedeemTransaction(rate);
						runtime.Services.TrustedBroadcastService.Broadcast(cycle.Start, Services.TransactionType.ClientRedeem, new CorrelationId(solver.Id), broadcast);
					}
				}
			}
		}
	}
}
