﻿using Microsoft.Extensions.Logging;
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
	public class Program
	{
		private static BroadcasterJob _broadcaster;
		private static ClassicTumblerParameters _parameters;
		private static DBreezeRepository _dbreeze;
		private static TumblerClient _client;
		private static IDestinationWallet _destinationWallet;
		private static ExternalServices _services;

		public static void Main(string[] args)
		{
			if (!args.Contains("-i", StringComparer.OrdinalIgnoreCase))
			{
				StartTumbler(args);
				return;
			}
			StartInteractiveMode(args);
		}

		private static void StartInteractiveMode(string[] args)
		{
			Console.Write(Assembly.GetEntryAssembly().GetName().Name
						+ " " + Assembly.GetEntryAssembly().GetName().Version);
			Console.WriteLine(" -- TumbleBit Implementation in .NET Core");
			Console.WriteLine("Type \"help\" for more information.");

			while (true)
			{
				Console.Write(">>> ");
				var split = Console.ReadLine().Split(null);
				Parser.Default.ParseArguments<TumbleOptions, StatusOptions, QuitOptions>(split)
					.WithParsed<TumbleOptions>(_ => StartTumbler(args))
					.WithParsed<StatusOptions>(_ => GetStatus())
					.WithParsed<QuitOptions>(_ => Environment.Exit(0));
			}
		}

		private static void GetStatus()
		{
			if (_broadcaster == null)
			{
				Console.WriteLine("Tumbler not initialized!");
				Console.WriteLine("Try to \"tumble\" first.");
				Console.WriteLine();
				return;
			}

			Logs.Configure(new FuncLoggerFactory(i => new ConsoleLogger(i, (a, b) => true, false)));
			var broadcasterCancel = new CancellationTokenSource();
			try
			{
				_broadcaster.Start(broadcasterCancel.Token);
				Logs.Main.LogInformation("BroadcasterJob started");
				var stateMachine = new StateMachinesExecutor(_parameters, _client, _destinationWallet, _services,
					_dbreeze,
					Logs.Main);
				stateMachine.Start(broadcasterCancel.Token);
				Logs.Main.LogInformation("State machines started");

				Logs.Main.LogInformation("Press enter to stop");
				Console.ReadLine();
				broadcasterCancel.Cancel();
			}
			catch (Exception ex)
			{
				Logs.Configuration.LogError(ex.Message);
				Logs.Configuration.LogDebug(ex.StackTrace);
			}
			finally
			{
				if (!broadcasterCancel.IsCancellationRequested)
					broadcasterCancel.Cancel();
			}
		}

		private static void StartTumbler(string[] args)
		{
			if (_broadcaster != null)
			{
				Console.WriteLine("Tumbler already initialized!");
				Console.WriteLine("Try to check the \"status\".");
				Console.WriteLine();
				return;
			}

			Logs.Configure(new FuncLoggerFactory(i => new ConsoleLogger(i, (a, b) => true, false)));
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
				_dbreeze = new DBreezeRepository(Path.Combine(dataDir, "db"));

				_services = ExternalServices.CreateFromRPCClient(rpc, _dbreeze);

				_broadcaster = new BroadcasterJob(_services, Logs.Main);
				_broadcaster.Start(broadcasterCancel.Token);
				Logs.Main.LogInformation("BroadcasterJob started");

				if(!onlymonitor)
				{
					var server = config.GetOrDefault("tumbler.server", null as Uri);
					if(server == null)
					{
						Logs.Main.LogError("tumbler.server not configured");
						throw new ConfigException();
					}
					_client = new TumblerClient(network, server);
					Logs.Configuration.LogInformation("Downloading tumbler information of " + server.AbsoluteUri);
					_parameters = Retry(3, () => _client.GetTumblerParameters());
					Logs.Configuration.LogInformation("Tumbler Server Connection successfull");
					var existingConfig = _dbreeze.Get<ClassicTumbler.ClassicTumblerParameters>("Configuration", _client.Address.AbsoluteUri);
					if(existingConfig != null)
					{
						if(Serializer.ToString(existingConfig) != Serializer.ToString(_parameters))
						{
							Logs.Configuration.LogError("The configuration file of the tumbler changed since last connection, it should never happen");
							throw new ConfigException();
						}
					}
					else
					{
						_dbreeze.UpdateOrInsert("Configuration", _client.Address.AbsoluteUri, _parameters, (o, n) => n);
					}

					if(_parameters.Network != rpc.Network)
					{
						throw new ConfigException("The tumbler server run on a different network than the local rpc server");
					}

					//IDestinationWallet destinationWallet = null;
					try
					{
						_destinationWallet = GetDestinationWallet(config, rpc.Network, _dbreeze);
					}
					catch(Exception ex)
					{
						Logs.Main.LogInformation("outputwallet.extpubkey is not configured, trying to use outputwallet.rpc settings.");
						try
						{
							_destinationWallet = GetRPCDestinationWallet(config, rpc.Network);
						}
						catch { throw ex; } //Not a bug, want to throw the other exception

					}
					var stateMachine = new StateMachinesExecutor(_parameters, _client, _destinationWallet, _services, _dbreeze, Logs.Main);
					stateMachine.Start(broadcasterCancel.Token);
					Logs.Main.LogInformation("State machines started");
				}
				Logs.Main.LogInformation("Press enter to stop");
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
				File.WriteAllText(config, builder.ToString());
			}
			return config;
		}
	}
}
