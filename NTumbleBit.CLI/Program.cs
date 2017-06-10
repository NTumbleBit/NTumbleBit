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

namespace NTumbleBit.CLI
{
	public partial class Program
	{
		public Tracker Tracker
		{
			get; set;
		}
		public Network Network
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
			CancellationTokenSource broadcasterCancel = new CancellationTokenSource();
			DBreezeRepository dbreeze = null;
			try
			{
				Network = args.Contains("-testnet", StringComparer.OrdinalIgnoreCase) ? Network.TestNet :
				args.Contains("-regtest", StringComparer.OrdinalIgnoreCase) ? Network.RegTest :
				Network.Main;
				Logs.Configuration.LogInformation("Network: " + Network);

				var dataDir = DefaultDataDirectory.GetDefaultDirectory("NTumbleBit", Network);
				var consoleArgs = new TextFileConfiguration(args);
				var configFile = GetDefaultConfigurationFile(dataDir, Network);
				var config = TextFileConfiguration.Parse(File.ReadAllText(configFile));
				consoleArgs.MergeInto(config, true);
				config.AddAlias("server", "tumbler.server");

				var onlymonitor = config.GetOrDefault<bool>("onlymonitor", false);

				RPCClient rpc = null;
				try
				{
					rpc = RPCArgs.ConfigureRPCClient(config, Network);
				}
				catch
				{
					throw new ConfigException("Please, fix rpc settings in " + configFile);
				}
				dbreeze = new DBreezeRepository(Path.Combine(dataDir, "db"));
				Tracker = new Tracker(dbreeze);
				var services = ExternalServices.CreateFromRPCClient(rpc, dbreeze, Tracker);

				var broadcaster = new BroadcasterJob(services, Logs.Main);
				broadcaster.Start(broadcasterCancel.Token);
				Logs.Main.LogInformation("BroadcasterJob started");

				if(!onlymonitor)
				{
					var server = config.GetOrDefault("tumbler.server", null as Uri);
					if(server == null)
					{
						Logs.Main.LogError("tumbler.server not configured");
						throw new ConfigException();
					}
					var client = new TumblerClient(Network, server);
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

					IDestinationWallet destinationWallet = null;
					try
					{
						destinationWallet = GetDestinationWallet(config, rpc.Network, dbreeze);
					}
					catch(Exception ex)
					{
						Logs.Main.LogInformation("outputwallet.extpubkey is not configured, trying to use outputwallet.rpc settings.");
						try
						{
							destinationWallet = GetRPCDestinationWallet(config, rpc.Network);
						}
						catch { throw ex; } //Not a bug, want to throw the other exception

					}
					var stateMachine = new StateMachinesExecutor(parameters, client, destinationWallet, services, dbreeze, Logs.Main, Tracker);
					stateMachine.Start(broadcasterCancel.Token);
					Logs.Main.LogInformation("State machines started");
				}


				StartInteractive();

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
				dbreeze?.Dispose();
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

		private static RPCDestinationWallet GetRPCDestinationWallet(TextFileConfiguration config, Network network)
		{
			var rpc = RPCArgs.ConfigureRPCClient(config, network, "outputwallet");
			return new RPCDestinationWallet(rpc);
		}

		private static ClientDestinationWallet GetDestinationWallet(TextFileConfiguration config, Network network, DBreezeRepository dbreeze)
		{
			BitcoinExtPubKey pubKey = null;
			KeyPath keypath = new KeyPath("0");
			try
			{
				pubKey = new BitcoinExtPubKey(config.GetOrDefault("outputwallet.extpubkey", null as string), network);
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
			return destinationWallet;
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
				builder.AppendLine("#tumbler.server=");
				builder.AppendLine("#outputwallet.extpubkey=");
				builder.AppendLine("#outputwallet.keypath=");
				File.WriteAllText(config, builder.ToString());
			}
			return config;
		}
	}
}
