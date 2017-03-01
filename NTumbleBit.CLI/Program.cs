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

namespace NTumbleBit.CLI
{
	public class Program
	{
		public static void Main(string[] args)
		{
			Logs.Configure(new FuncLoggerFactory(i => new ConsoleLogger("Configuration", (a, b) => true, false)));
			var logger = new ConsoleLogger("Configuration", (a, b) => true, false);
			CancellationTokenSource broadcasterCancel = new CancellationTokenSource();
			try
			{
				var network = args.Contains("-testnet", StringComparer.OrdinalIgnoreCase) ? Network.TestNet :
				args.Contains("-regtest", StringComparer.OrdinalIgnoreCase) ? Network.RegTest :
				Network.Main;
				Logs.Configuration.LogInformation("Network: " + network);

				var dataDir = DefaultDataDirectory.GetDefaultDirectory("NTumbleBit", network);
				var consoleArgs = new TextFileConfiguration(args);
				var configFile = GetDefaultConfigurationFile(dataDir, network);
				var config = TextFileConfiguration.Parse(File.ReadAllText(configFile));
				consoleArgs.MergeInto(config, true);
				config.AddAlias("server", "tumbler.server");

				var onlymonitor = config.GetOrDefault<bool>("onlymonitor", false);

				RPCClient rpc = null;
				try
				{
					rpc = RPCArgs.ConfigureRPCClient(config, network);
				}
				catch
				{
					throw new ConfigException("Please, fix rpc settings in " + configFile);
				}
				var dbreeze = new DBreezeRepository(Path.Combine(dataDir, "db"));

				var services = ExternalServices.CreateFromRPCClient(rpc, dbreeze);
				
				var broadcaster = new BroadcasterJob(services, logger);
				broadcaster.Start(broadcasterCancel.Token);
				Logs.Configuration.LogInformation("Monitor started");

				if(!onlymonitor)
				{
					var server = config.GetOrDefault("tumbler.server", null as Uri);
					if(server == null)
					{
						Logs.Main.LogError("tumbler.server not configured");
						throw new ConfigException();
					}
					var client = new TumblerClient(network, server);
					Logs.Configuration.LogInformation("Downloading tumbler information of " + server.AbsoluteUri);
					var parameters = Retry(3, () => client.GetTumblerParameters());
					Logs.Configuration.LogInformation("Tumbler Server Connection successfull");
					var existingConfig = dbreeze.Get<ClassicTumbler.ClassicTumblerParameters>("Configuration", client.Address.AbsoluteUri);
					if(existingConfig != null)
					{
						if(Serializer.ToString(existingConfig) != Serializer.ToString(parameters))
						{
							Logs.Configuration.LogError("The configuration file of the tumbler changed since last connection, it should never happen");
							throw new ConfigException();
						}
					}
					else
					{
						dbreeze.UpdateOrInsert("Configuration", client.Address.AbsoluteUri, parameters, (o, n) => n);
					}

					if(parameters.Network != rpc.Network)
					{
						throw new ConfigException("The tumbler server run on a different network than the local rpc server");
					}

					BitcoinExtPubKey pubKey = null;
					KeyPath keypath = new KeyPath("0");
					try
					{
						pubKey = new BitcoinExtPubKey(config.GetOrDefault("outputwallet.extpubkey", null as string), rpc.Network);
					}
					catch
					{
						throw new ConfigException("outputwallet.extpubkey is not configured correctly");
					}

					string keyPathString = config.GetOrDefault("outputwallet.keypath", null as string);
					if(keyPathString != null)
					{
						try
						{
							keypath = new KeyPath(keyPathString);
						}
						catch
						{
							throw new ConfigException("outputwallet.keypath is not configured correctly");
						}
					}
					var destinationWallet = new ClientDestinationWallet("", pubKey, keypath, dbreeze);
					var stateMachine = new StateMachinesExecutor(parameters, client, destinationWallet, services, dbreeze, logger);
					stateMachine.Start(broadcasterCancel.Token);
					Logs.Configuration.LogInformation("State machines started");
				}
				Logs.Configuration.LogInformation("Press enter to stop");
				Console.ReadLine();
				broadcasterCancel.Cancel();
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
				if(!broadcasterCancel.IsCancellationRequested)
					broadcasterCancel.Cancel();
			}
		}

		private static T Retry<T>(int count, Func<T> act)
		{
			var exceptions = new List<Exception>();
			for(int i = 0; i < count; i++)
			{
				try
				{
					return act();
				}
				catch(Exception ex)
				{
					exceptions.Add(ex);
				}
			}
			throw new AggregateException(exceptions);
		}

		public static string GetDefaultConfigurationFile(string dataDirectory, Network network)
		{
			var config = Path.Combine(dataDirectory, "client.config");
			Logs.Configuration.LogInformation("Configuration file set to " + config);
			if(!File.Exists(config))
			{
				Logs.Configuration.LogInformation("Creating configuration file");
				StringBuilder builder = new StringBuilder();
				builder.AppendLine("#rpc.url=http://localhost:" + network.RPCPort + "/");
				builder.AppendLine("#rpc.user=bitcoinuser");
				builder.AppendLine("#rpc.password=bitcoinpassword");
				builder.AppendLine("#rpc.cookiefile=yourbitcoinfolder/.cookie");
				File.WriteAllText(config, builder.ToString());
			}
			return config;
		}
	}
}
