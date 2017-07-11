using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using NBitcoin;
using Microsoft.Extensions.Logging;
using NTumbleBit.Logging;
using System.Net;
using NTumbleBit.Configuration;
using System.Net.Sockets;
using System.Net.Http;
using DotNetTor.SocksPort;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler.Client
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

	public class ConnectionSettings
	{
		public virtual HttpMessageHandler CreateHttpHandler()
		{
			return null;
		}
	}

	public class TorConnectionSettings : ConnectionSettings
	{
		public enum ConnectionTest
		{
			AuthError,
			Success,
			SocketError
		}
		public IPEndPoint Server
		{
			get; set;
		}

		public string Password
		{
			get; set;
		}

		public string CookieFile
		{
			get; set;
		}

		public override HttpMessageHandler CreateHttpHandler()
		{
			CancellationTokenSource cts = new CancellationTokenSource();
			cts.CancelAfter(60000);
			CreateTorClient().ChangeCircuitAsync(cts.Token).GetAwaiter().GetResult();
			return new SocksPortHandler(_SocksEndpoint);
		}

		public void AutoDetectCookieFile()
		{
			FileInfo cookie = null;
			if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				cookie = new FileInfo("/var/lib/tor/control_auth_cookie");
				cookie = cookie.Exists ? cookie : new FileInfo("/var/run/tor/control.authcookie");
			}
			else
			{
				var localAppData = Environment.GetEnvironmentVariable("APPDATA");
				cookie = new FileInfo(Path.Combine(localAppData, "tor", "control_auth_cookie"));
			}

			if(!cookie.Exists)
				throw new ConfigException("NTumbleBit could not find any tor control cookie");
			CookieFile = cookie.FullName;
			try
			{
				File.ReadAllBytes(CookieFile);
			}
			catch(Exception ex)
			{
				throw new ConfigException("Error while reading tor cookie file " + ex.Message);
			}
		}

		IPEndPoint _SocksEndpoint;
		async Task<ConnectionTest> TryConnectAsync()
		{
			var tor = CreateTorClient();
			try
			{
				await tor.IsCircuitEstabilishedAsync().ConfigureAwait(false);
				if(_SocksEndpoint == null)
				{
					var result = await tor.SendCommandAsync("GETINFO net/listeners/socks", default(CancellationToken)).ConfigureAwait(false);
					var match = Regex.Match(result, @"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}):(\d{1,5})");
					if(!match.Success)
						throw new ConfigException("No socks port are exposed by Tor");
					_SocksEndpoint = new IPEndPoint(IPAddress.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
				}
				return ConnectionTest.Success;
			}
			catch(Exception ex)
			{
				if(IsSocketException(ex))
				{
					return ConnectionTest.SocketError;
				}
				return ConnectionTest.AuthError;
			}
		}

		private bool IsSocketException(Exception ex)
		{
			while(ex != null)
			{
				if(ex is SocketException)
					return true;
				ex = ex.InnerException;
			}
			return false;
		}

		DotNetTor.ControlPort.Client CreateTorClient()
		{
			if(string.IsNullOrEmpty(Password) && string.IsNullOrEmpty(CookieFile))
			{
				return new DotNetTor.ControlPort.Client(Server.Address.ToString(), Server.Port);
			}
			if(!string.IsNullOrEmpty(Password))
			{
				return new DotNetTor.ControlPort.Client(Server.Address.ToString(), Server.Port, Password);
			}
			if(!string.IsNullOrEmpty(CookieFile))
			{
				return new DotNetTor.ControlPort.Client(Server.Address.ToString(), Server.Port, new FileInfo(CookieFile));
			}
			else
				throw new ConfigException("Invalid Tor configuration");
		}

		internal async Task SetupAsync()
		{
			var autoConfig = string.IsNullOrEmpty(Password) && String.IsNullOrEmpty(CookieFile);
			var connectResult = await TryConnectAsync().ConfigureAwait(false);
			if(connectResult == TorConnectionSettings.ConnectionTest.SocketError)
				throw new ConfigException("Unable to connect to tor control port");
			else if(connectResult == TorConnectionSettings.ConnectionTest.Success)
				return;
			else if(!autoConfig && connectResult == TorConnectionSettings.ConnectionTest.AuthError)
				throw new ConfigException("Unable to authenticate tor control port");

			if(autoConfig)
				AutoDetectCookieFile();
			connectResult = await TryConnectAsync().ConfigureAwait(false);
			if(connectResult != TorConnectionSettings.ConnectionTest.Success)
				throw new ConfigException("Unable to authenticate tor control port");
		}
	}

	public class HttpConnectionSettings : ConnectionSettings
	{
		class CustomProxy : IWebProxy
		{
			private Uri _Address;

			public CustomProxy(Uri address)
			{
				if(address == null)
					throw new ArgumentNullException("address");
				_Address = address;
			}

			public Uri GetProxy(Uri destination)
			{
				return _Address;
			}

			public bool IsBypassed(Uri host)
			{
				return false;
			}

			public ICredentials Credentials
			{
				get; set;
			}
		}
		public Uri Proxy
		{
			get; set;
		}
		public NetworkCredential Credentials
		{
			get; set;
		}

		public override HttpMessageHandler CreateHttpHandler()
		{
			CustomProxy proxy = new CustomProxy(Proxy);
			proxy.Credentials = Credentials;
			HttpClientHandler handler = new HttpClientHandler();
			handler.Proxy = proxy;
			Utils.SetAntiFingerprint(handler);
			return handler;
		}
	}
	public class SocksConnectionSettings : ConnectionSettings
	{
		public IPEndPoint Proxy
		{
			get; set;
		}

		public override HttpMessageHandler CreateHttpHandler()
		{
			SocksPortHandler handler = new SocksPortHandler(Proxy);
			return handler;
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

		public ConnectionSettings BobConnectionSettings
		{
			get; set;
		} = new ConnectionSettings();

		public ConnectionSettings AliceConnectionSettings
		{
			get; set;
		} = new ConnectionSettings();

		public OutputWalletConfiguration OutputWallet
		{
			get; set;
		} = new OutputWalletConfiguration();

		public RPCArgs RPCArgs
		{
			get; set;
		} = new RPCArgs();
		public bool AllowInsecure
		{
			get;
			set;
		} = false;

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

			AliceConnectionSettings = ParseConnectionSettings("alice", config);
			BobConnectionSettings = ParseConnectionSettings("bob", config);

			AllowInsecure = config.GetOrDefault<bool>("allowinsecure", IsTest(Network));
			return this;
		}

		private bool IsTest(Network network)
		{
			return network == Network.TestNet || network == Network.RegTest;
		}

		private ConnectionSettings ParseConnectionSettings(string prefix, TextFileConfiguration config, string defaultType = "tor")
		{
			var type = config.GetOrDefault<string>(prefix + ".proxy.type", defaultType);
			if(type.Equals("none", StringComparison.OrdinalIgnoreCase))
			{
				return new ConnectionSettings();
			}
			else if(type.Equals("http", StringComparison.OrdinalIgnoreCase))
			{

				HttpConnectionSettings settings = new HttpConnectionSettings();
				var server = config.GetOrDefault<Uri>(prefix + ".proxy.server", null);
				if(server != null)
					settings.Proxy = server;
				var user = config.GetOrDefault<string>(prefix + ".proxy.username", null);
				var pass = config.GetOrDefault<string>(prefix + ".proxy.password", null);
				if(user != null && pass != null)
					settings.Credentials = new NetworkCredential(user, pass);
				return settings;
			}
			else if(type.Equals("socks", StringComparison.OrdinalIgnoreCase))
			{
				SocksConnectionSettings settings = new SocksConnectionSettings();
				var server = config.GetOrDefault<IPEndPoint>(prefix + ".proxy.server", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050));
				settings.Proxy = server;
				return settings;
			}
			else if(type.Equals("tor", StringComparison.OrdinalIgnoreCase))
			{
				TorConnectionSettings settings = new TorConnectionSettings();
				settings.Server = config.GetOrDefault<IPEndPoint>(prefix + ".proxy.server", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9051));
				settings.Password = config.GetOrDefault<string>(prefix + ".proxy.password", null);
				settings.CookieFile = config.GetOrDefault<string>(prefix + ".proxy.cookiefile", null);
				return settings;
			}
			else
				throw new ConfigException(prefix + ".proxy.type is not supported, should be socks or http");
		}

		public static string GetDefaultConfigurationFile(string dataDirectory, Network network)
		{
			var config = Path.Combine(dataDirectory, "client.config");
			Logs.Configuration.LogInformation("Configuration file set to " + config);
			if(!File.Exists(config))
			{
				Logs.Configuration.LogInformation("Creating configuration file");
				StringBuilder builder = new StringBuilder();
				builder.AppendLine("####Common Commands####");
				builder.AppendLine("#Connection to the input wallet. TumbleBit.CLI will try to autoconfig based on default settings of Bitcoin Core.");
				builder.AppendLine("#rpc.url=http://localhost:" + network.RPCPort + "/");
				builder.AppendLine("#rpc.user=bitcoinuser");
				builder.AppendLine("#rpc.password=bitcoinpassword");
				builder.AppendLine("#rpc.cookiefile=yourbitcoinfolder/.cookie");

				builder.AppendLine("#Tumbler server to connect to");
				builder.AppendLine("#tumbler.server=");
				builder.AppendLine();
				builder.AppendLine("#Configuration of the output wallet");
				builder.AppendLine("#outputwallet.extpubkey=xpub");
				builder.AppendLine("#outputwallet.keypath=0");
				builder.AppendLine();
				builder.AppendLine();
				builder.AppendLine("####Connection Commands####");
				builder.AppendLine("#Making Alice or Bob pass through TOR (Recommended, the circuit will change for each cycle/persona)");
				builder.AppendLine("#The default settings you run `tor -controlport 9051 -cookieauthentication 1`");
				builder.AppendLine("#alice.proxy.type=tor");
				builder.AppendLine("#alice.proxy.server=127.0.0.1:9051");
				builder.AppendLine("#alice.proxy.password=padeiwmnfw");
				builder.AppendLine("#alice.proxy.cookiefile=/var/run/tor/control.authcookie");
				builder.AppendLine("#or");
				builder.AppendLine("#bob.proxy.type=tor");
				builder.AppendLine("#bob.proxy.server=127.0.0.1:9051");
				builder.AppendLine("#bob.proxy.password=padeiwmnfw");
				builder.AppendLine("#bob.proxy.cookiefile=/var/run/tor/control.authcookie");
				builder.AppendLine();
				builder.AppendLine("#Making Alice or Bob pass through a HTTP Proxy");
				builder.AppendLine("#alice.proxy.type=http");
				builder.AppendLine("#alice.proxy.server=http://127.0.0.1:8118/");
				builder.AppendLine("#alice.proxy.username=dpowqkwkpd");
				builder.AppendLine("#alice.proxy.password=padeiwmnfw");
				builder.AppendLine("#or");
				builder.AppendLine("#bob.proxy.type=http");
				builder.AppendLine("#bob.proxy.server=http://127.0.0.1:8118/");
				builder.AppendLine("#bob.proxy.username=dpowqkwkpd");
				builder.AppendLine("#bob.proxy.password=padeiwmnfw");
				builder.AppendLine();
				builder.AppendLine("#Making Alice or Bob pass through a SOCKS Proxy");
				builder.AppendLine("#alice.proxy.type=socks");
				builder.AppendLine("#alice.proxy.server=127.0.0.1:9050");
				builder.AppendLine("#or");
				builder.AppendLine("#bob.proxy.type=socks");
				builder.AppendLine("#bob.proxy.server=127.0.0.1:9050");
				builder.AppendLine();
				builder.AppendLine("#Disabling any proxy");
				builder.AppendLine("#alice.proxy.type=none");
				builder.AppendLine("#or");
				builder.AppendLine("#bob.proxy.type=none");


				builder.AppendLine();
				builder.AppendLine();

				builder.AppendLine("####Debug Commands####");
				builder.AppendLine("#Whether or not signature for the escape transaction is transmitted to the Tumbler (default: true)");
				builder.AppendLine("#cooperative=false");
				builder.AppendLine("#Whether or not IP sharing between Bob and Alice is authorized (default: true for testnets, false for mainnet)");
				builder.AppendLine("#allowinsecure=true");
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
