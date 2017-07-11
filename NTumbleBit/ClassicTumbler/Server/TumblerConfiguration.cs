using NBitcoin;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NTumbleBit.Logging;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Runtime.InteropServices;
using NTumbleBit.Configuration;
using System.Diagnostics;

namespace NTumbleBit.ClassicTumbler.Server
{

	public class TumblerConfiguration
	{
		public TumblerConfiguration()
		{
			ClassicTumblerParameters = new ClassicTumblerParameters();
		}

		public string DataDir
		{
			get; set;
		}
		
		public Network Network
		{
			get
			{
				return ClassicTumblerParameters.Network;
			}
			set
			{
				ClassicTumblerParameters.Network = value;
			}
		}

		public ClassicTumblerParameters ClassicTumblerParameters
		{
			get; set;
		}
		public string ConfigurationFile
		{
			get;
			set;
		}

		public RPCArgs RPC
		{
			get; set;
		} = new RPCArgs();

		public List<IPEndPoint> Listen
		{
			get;
			set;
		} = new List<IPEndPoint>();
		public bool OnlyMonitor
		{
			get;
			set;
		}
		public bool Cooperative
		{
			get;
			set;
		}

		public TumblerConfiguration LoadArgs(String[] args)
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
				DataDir = DefaultDataDirectory.GetDefaultDirectory("NTumbleBitServer", Network);
			}

			if(ConfigurationFile == null)
			{
				ConfigurationFile = GetDefaultConfigurationFile();
			}
			Logs.Configuration.LogInformation("Network: " + Network);
			if(Network == Network.TestNet)
			{
				var cycle = ClassicTumblerParameters
							.CycleGenerator.FirstCycle;
				cycle.Start = 0;
				cycle.RegistrationDuration = 3;
				cycle.ClientChannelEstablishmentDuration = 3;
				cycle.TumblerChannelEstablishmentDuration = 3;
				cycle.SafetyPeriodDuration = 2;
				cycle.PaymentPhaseDuration = 3;
				cycle.TumblerCashoutDuration = 4;
				cycle.ClientCashoutDuration = 3;
			}

			Logs.Configuration.LogInformation("Data directory set to " + DataDir);
			Logs.Configuration.LogInformation("Configuration file set to " + ConfigurationFile);

			if(!Directory.Exists(DataDir))
				throw new ConfigurationException("Data directory does not exists");

			var consoleConfig = new TextFileConfiguration(args);
			var config = TextFileConfiguration.Parse(File.ReadAllText(ConfigurationFile));
			consoleConfig.MergeInto(config, true);

			if (config.Contains("help"))
			{
				Console.WriteLine("Details on the wiki page :  https://github.com/NTumbleBit/NTumbleBit/wiki/Server-Config");
				OpenBrowser("https://github.com/NTumbleBit/NTumbleBit/wiki/Server-Config");
				Environment.Exit(0);
			}

			Cooperative = config.GetOrDefault<bool>("cooperative", true);

			var defaultPort = config.GetOrDefault<int>("port", 37123);

			OnlyMonitor = config.GetOrDefault<bool>("onlymonitor", false);
			Listen = config
						.GetAll("bind")
						.Select(p => ConvertToEndpoint(p, defaultPort))
						.ToList();
			if(Listen.Count == 0)
			{
				Listen.Add(new IPEndPoint(IPAddress.Any, defaultPort));
			}

			RPC = RPCArgs.Parse(config, Network);
			return this;
		}		

		public string GetDefaultConfigurationFile()
		{
			var config = Path.Combine(DataDir, "server.config");
			Logs.Configuration.LogInformation("Configuration file set to " + config);
			if(!File.Exists(config))
			{
				Logs.Configuration.LogInformation("Creating configuration file");
				StringBuilder builder = new StringBuilder();
				builder.AppendLine("####Common Commands####");
				builder.AppendLine("#Connection to the input wallet. TumbleBit.CLI will try to autoconfig based on default settings of Bitcoin Core.");
				builder.AppendLine("#rpc.url=http://localhost:" + Network.RPCPort + "/");
				builder.AppendLine("#rpc.user=bitcoinuser");
				builder.AppendLine("#rpc.password=bitcoinpassword");
				builder.AppendLine("#rpc.cookiefile=yourbitcoinfolder/.cookie");

				builder.AppendLine();
				builder.AppendLine();

				builder.AppendLine("####Server Commands####");
				builder.AppendLine("#port=37123");
				builder.AppendLine("#listen=0.0.0.0");

				builder.AppendLine();
				builder.AppendLine();

				builder.AppendLine("####Debug Commands####");
				builder.AppendLine("#Whether or not the tumbler deliver puzzle's solution off chain to the client (default: true)");
				builder.AppendLine("#cooperative=false");
				builder.AppendLine("#Whether or not IP sharing between Bob and Alice is authorized (default: true for testnets, false for mainnet)");
				builder.AppendLine("#allowinsecure=true");
				File.WriteAllText(config, builder.ToString());
			}
			return config;
		}

		public string[] GetUrls()
		{
			return Listen.Select(b => "http://" + b + "/").ToArray();
		}

		public static IPEndPoint ConvertToEndpoint(string str, int defaultPort)
		{
			var portOut = defaultPort;
			var hostOut = "";
			int colon = str.LastIndexOf(':');
			// if a : is found, and it either follows a [...], or no other : is in the string, treat it as port separator
			bool fHaveColon = colon != -1;
			bool fBracketed = fHaveColon && (str[0] == '[' && str[colon - 1] == ']'); // if there is a colon, and in[0]=='[', colon is not 0, so in[colon-1] is safe
			bool fMultiColon = fHaveColon && (str.LastIndexOf(':', colon - 1) != -1);
			if(fHaveColon && (colon == 0 || fBracketed || !fMultiColon))
			{
				int n;
				if(int.TryParse(str.Substring(colon + 1), out n) && n > 0 && n < 0x10000)
				{
					str = str.Substring(0, colon);
					portOut = n;
				}
			}
			if(str.Length > 0 && str[0] == '[' && str[str.Length - 1] == ']')
				hostOut = str.Substring(1, str.Length - 2);
			else
				hostOut = str;
			return new IPEndPoint(IPAddress.Parse(hostOut), portOut);
		}

		private void AssetConfigFileExists()
		{
			if(!File.Exists(ConfigurationFile))
				throw new ConfigurationException("Configuration file does not exists");
		}
		
		public void OpenBrowser(string url)
		{
			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					Process.Start(new ProcessStartInfo("cmd", $"/c start {url}")); // Works ok on windows
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					Process.Start("xdg-open", url);  // Works ok on linux
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					Process.Start("open", url); // Not tested
				}
			}
			catch(Exception)
			{
				// nothing happens
			}
		}
	}
}

