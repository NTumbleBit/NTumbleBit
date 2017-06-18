using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using NBitcoin;
using Microsoft.Extensions.Logging;
using NTumbleBit.Common;
using NTumbleBit.Common.Logging;
using NTumbleBit.Client.Tumbler.Services;

namespace NTumbleBit.Client.Tumbler
{
	public class OutputWalletConfiguration
	{
		public BitcoinExtPubKey RootKey
		{
			get; set;
		}

		public KeyPath KeyPath
		{
			get; set;
		}

		public RPCArgs RPCArgs
		{
			get; set;
		}
	}

	public class TumblerClientConfiguration
	{
		public string ConfigurationFile
		{
			get;
			set;
		}
		public string DataDir
		{
			get;
			set;
		}

		public Network Network
		{
			get; set;
		}

		public bool OnlyMonitor
		{
			get; set;
		}

		public bool CheckIp
		{
			get; set;
		} = true;

		public bool Cooperative
		{
			get;
			set;
		}
		public Uri TumblerServer
		{
			get;
			set;
		}

		public OutputWalletConfiguration OutputWallet
		{
			get; set;
		} = new OutputWalletConfiguration();

		public RPCArgs RPCArgs
		{
			get; set;
		} = new RPCArgs();

		public TumblerClientConfiguration LoadArgs(String[] args)
		{
			ConfigurationFile = args.Where(a => a.StartsWith("-conf=", StringComparison.Ordinal)).Select(a => a.Substring("-conf=".Length).Replace("\"", "")).FirstOrDefault();
			DataDir = args.Where(a => a.StartsWith("-datadir=", StringComparison.Ordinal)).Select(a => a.Substring("-datadir=".Length).Replace("\"", "")).FirstOrDefault();
			if(DataDir != null && ConfigurationFile != null)
			{
				var isRelativePath = Path.GetFullPath(ConfigurationFile).Length > ConfigurationFile.Length;
				if(isRelativePath)
				{
					ConfigurationFile = Path.Combine(DataDir, ConfigurationFile);
				}
			}

			Network = args.Contains("-testnet", StringComparer.OrdinalIgnoreCase) ? Network.TestNet :
				args.Contains("-regtest", StringComparer.OrdinalIgnoreCase) ? Network.RegTest :
				Network.Main;

			if(ConfigurationFile != null)
			{
				AssetConfigFileExists();
				var configTemp = TextFileConfiguration.Parse(File.ReadAllText(ConfigurationFile));
				Network = configTemp.GetOrDefault<bool>("testnet", false) ? Network.TestNet :
						  configTemp.GetOrDefault<bool>("regtest", false) ? Network.RegTest :
						  Network.Main;
			}

			if(DataDir == null)
			{
				DataDir = DefaultDataDirectory.GetDefaultDirectory("NTumbleBit", Network);
			}

			if(ConfigurationFile == null)
			{
				ConfigurationFile = GetDefaultConfigurationFile(DataDir, Network);
			}
			Logs.Configuration.LogInformation("Network: " + Network);

			Logs.Configuration.LogInformation("Data directory set to " + DataDir);
			Logs.Configuration.LogInformation("Configuration file set to " + ConfigurationFile);

			if(!Directory.Exists(DataDir))
				throw new ConfigurationException("Data directory does not exists");

			var consoleConfig = new TextFileConfiguration(args);
			var config = TextFileConfiguration.Parse(File.ReadAllText(ConfigurationFile));
			consoleConfig.MergeInto(config, true);
			config.AddAlias("server", "tumbler.server");

			OnlyMonitor = config.GetOrDefault<bool>("onlymonitor", false);
			Cooperative = config.GetOrDefault<bool>("cooperative", true);
			TumblerServer = config.GetOrDefault("tumbler.server", null as Uri);
			RPCArgs = RPCArgs.Parse(config, Network);

			if(!OnlyMonitor && TumblerServer == null)
				throw new ConfigException("tumbler.server not configured");

			try
			{
				var key = config.GetOrDefault("outputwallet.extpubkey", null as string);
				if(key != null)
					OutputWallet.RootKey = new BitcoinExtPubKey(key, Network);
			}
			catch
			{
				throw new ConfigException("outputwallet.extpubkey is not configured correctly");
			}

			OutputWallet.KeyPath = new KeyPath("0");
			string keyPathString = config.GetOrDefault("outputwallet.keypath", null as string);
			if(keyPathString != null)
			{
				try
				{
					OutputWallet.KeyPath = new KeyPath(keyPathString);
				}
				catch
				{
					throw new ConfigException("outputwallet.keypath is not configured correctly");
				}
			}

			if(OutputWallet.KeyPath.ToString().Contains("'"))
				throw new ConfigException("outputwallet.keypath should not contain any hardened derivation");

			if(OutputWallet.RootKey != null && OutputWallet.RootKey.Network != Network)
				throw new ConfigException("outputwallet.extpubkey is pointing an incorrect network");

			OutputWallet.RPCArgs = RPCArgs.Parse(config, Network, "outputwallet");

			return this;
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

				builder.AppendLine("####Advanced Comands");
				builder.AppendLine("#cooperative=false");
				File.WriteAllText(config, builder.ToString());
			}
			return config;
		}

		private void AssetConfigFileExists()
		{
			if(!File.Exists(ConfigurationFile))
				throw new ConfigurationException("Configuration file does not exists");
		}

	}
}
