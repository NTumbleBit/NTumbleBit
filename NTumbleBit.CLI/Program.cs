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

namespace NTumbleBit.CLI
{
	public class Program
	{
		public static void Main(string[] args)
		{
			ConsoleLogger logger = new ConsoleLogger("Configuration", (a, b) => true, false);
			try
			{

				Network network = Network.Main;
				CommandLineParser parser = new CommandLineParser(args);
				if(parser.GetBool("-testnet"))
					network = Network.TestNet;
				var onlymonitor = parser.GetBool("-onlymonitor");
				var dataDir = DefaultDataDirectory.GetDefaultDirectory("NTumbleBit", logger, network);
				var configurationFile = GetDefaultConfigurationFile(logger, dataDir, network);
				var rpc = RPCConfiguration.ConfigureRPCClient(logger, configurationFile, network);
				var dbreeze = new DBreezeRepository(Path.Combine(dataDir, "db"));
				var config = TextFileConfiguration.Parse(File.ReadAllText(configurationFile));


				var services = ExternalServices.CreateFromRPCClient(rpc, dbreeze);
				CancellationTokenSource source = new CancellationTokenSource();
				var broadcaster = new BroadcasterJob(services, logger);
				broadcaster.Start(source.Token);
				logger.LogInformation("Monitor started");

				if(!onlymonitor)
				{
					var server = config.TryGet("tumbler.server");
					if(server == null)
					{
						logger.LogError("tumbler.server not configured");
						throw new ConfigException();
					}
					var client = new NTumbleBit.Client.Tumbler.TumblerClient(network, new Uri(server));
					logger.LogInformation("Downloading tumbler information");
					var parameters = Retry(3, () => client.GetTumblerParameters());
					logger.LogInformation("Tumbler Server Connection successfull");
					var existingConfig = dbreeze.Get<ClassicTumbler.ClassicTumblerParameters>("Configuration", client.Address.AbsoluteUri);
					if(existingConfig != null)
					{
						if(Serializer.ToString(existingConfig) != Serializer.ToString(parameters))
						{
							logger.LogError("The configuration file of the tumbler changed since last connection, it should never happen");
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
						pubKey = new BitcoinExtPubKey(config.TryGet("outputwallet.extpubkey"), rpc.Network);
					}
					catch
					{
						throw new ConfigException("outputwallet.extpubkey is not configured correctly");
					}

					string keyPathString = config.TryGet("outputwallet.keypath");
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
					stateMachine.Start(source.Token);
					logger.LogInformation("State machines started");
				}
				logger.LogInformation("Press enter to stop");
				Console.ReadLine();
				source.Cancel();
			}
			catch(ConfigException ex)
			{
				if(!string.IsNullOrEmpty(ex.Message))
					logger.LogError(ex.Message);
			}
			catch(Exception ex)
			{
				logger.LogError(ex.Message);
				logger.LogDebug(ex.StackTrace);
			}
		}

		static T Retry<T>(int count, Func<T> act)
		{
			Exception ex = null;
			for(int i = 0; i < count; i++)
			{
				try
				{
					return act();
				}
				catch(Exception exx) { ex = exx; }
			}
			throw ex;
		}

		public static string GetDefaultConfigurationFile(ILogger logger, string dataDirectory, Network network)
		{
			var config = Path.Combine(dataDirectory, "client.config");
			logger.LogInformation("Configuration file set to " + config);
			if(!File.Exists(config))
			{
				logger.LogInformation("Creating configuration file");

				var data = TextFileConfiguration.CreateClientDefaultConfiguration(network);
				File.WriteAllText(config, data);
			}
			return config;
		}
	}
}
