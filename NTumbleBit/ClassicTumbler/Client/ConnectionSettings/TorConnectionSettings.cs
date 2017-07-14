using DotNetTor.SocksPort;
using Microsoft.Extensions.Logging;
using System.Linq;
using NTumbleBit.ClassicTumbler.CLI;
using NTumbleBit.Configuration;
using NTumbleBit.Tor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using NTumbleBit.Logging;

namespace NTumbleBit.ClassicTumbler.Client.ConnectionSettings
{
	public class TorConnectionSettings : ConnectionSettingsBase
	{
		public static TorConnectionSettings ParseConnectionSettings(string prefix, TextFileConfiguration config)
		{
			TorConnectionSettings settings = new TorConnectionSettings();
			settings.Server = config.GetOrDefault<IPEndPoint>(prefix + ".server", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9051));
			settings.Password = config.GetOrDefault<string>(prefix + ".password", null);
			settings.CookieFile = config.GetOrDefault<string>(prefix + ".cookiefile", null);
			settings.VirtualPort = config.GetOrDefault<int>(prefix + ".virtualport", 80);
			return settings;
		}
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

		public int VirtualPort
		{
			get; set;
		}

		internal IPEndPoint SocksEndpoint
		{
			get; set;
		}

		public override HttpMessageHandler CreateHttpHandler()
		{
			CancellationTokenSource cts = new CancellationTokenSource();
			cts.CancelAfter(60000);
			var client = CreateTorClient();
			client.ChangeCircuitAsync(cts.Token).GetAwaiter().GetResult();
			while(!client.IsCircuitEstabilishedAsync(cts.Token).GetAwaiter().GetResult())
			{
				cts.Token.ThrowIfCancellationRequested();
				cts.Token.WaitHandle.WaitOne(100);
			}
			return new SocksPortHandler(SocksEndpoint);
		}

		public void AutoDetectCookieFile()
		{
			FileInfo cookie = null;
			if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				cookie = new FileInfo("/var/lib/tor/control_auth_cookie");
				cookie = cookie.Exists ? cookie : new FileInfo("/var/run/tor/control.authcookie");

				var home = Environment.GetEnvironmentVariable("HOME");
				if(!string.IsNullOrEmpty(home))
				{
					cookie = cookie.Exists ? cookie : new FileInfo(Path.Combine(home, ".tor", "control_auth_cookie"));
					cookie = cookie.Exists ? cookie : new FileInfo(Path.Combine(home, ".tor", "control.authcookie"));
				}
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

		async Task<ConnectionTest> TryConnectAsync()
		{
			using(var tor = CreateTorClient2())
			{
				try
				{
					await tor.ConnectAsync().ConfigureAwait(false);
				}
				catch { return ConnectionTest.SocketError; }

				if(!await tor.AuthenticateAsync().ConfigureAwait(false))
					return ConnectionTest.AuthError;
				if(SocksEndpoint == null)
				{
					var endpoints = await tor.GetSocksListenersAsync().ConfigureAwait(false);
					SocksEndpoint = endpoints.FirstOrDefault();
					if(SocksEndpoint == null)
						throw new TorException("Tor has no socks listener", "");
				}
				return ConnectionTest.Success;
			}
		}

		internal DotNetTor.ControlPort.Client CreateTorClient()
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

		internal TorClient CreateTorClient2()
		{
			if(string.IsNullOrEmpty(Password) && string.IsNullOrEmpty(CookieFile))
			{
				return new TorClient(Server.Address.ToString(), Server.Port);
			}
			if(!string.IsNullOrEmpty(Password))
			{
				return new TorClient(Server.Address.ToString(), Server.Port, Password);
			}
			if(!string.IsNullOrEmpty(CookieFile))
			{
				return new TorClient(Server.Address.ToString(), Server.Port, new FileInfo(CookieFile));
			}
			else
				throw new ConfigException("Invalid Tor configuration");
		}

		internal async Task<IDisposable> SetupAsync(ClientInteraction interaction, string torPath)
		{
			var autoConfig = string.IsNullOrEmpty(Password) && String.IsNullOrEmpty(CookieFile);
			var connectResult = await TryConnectAsync().ConfigureAwait(false);
			if(connectResult == TorConnectionSettings.ConnectionTest.SocketError)
			{
				if(torPath != null && autoConfig && Server.Address.Equals(IPAddress.Parse("127.0.0.1")))
				{
					var args = $"-controlport {Server.Port} -cookieauthentication 1";
					await interaction.AskConnectToTorAsync(torPath, args).ConfigureAwait(false);
					try
					{
						var processInfo = new ProcessStartInfo(torPath)
						{
							Arguments = args,
							UseShellExecute = false,
							CreateNoWindow = true,
							RedirectStandardOutput = false
						};
						var process = new ProcessDisposable(Process.Start(processInfo));
						try
						{
							await SetupAsync(interaction, null).ConfigureAwait(false);
						}
						catch
						{
							process.Dispose();
							throw;
						}
						return process;
					}
					catch(Exception ex)
					{
						Logs.Configuration.LogError($"Failed to start Tor, please verify your configuration settings \"torpath\": {ex.Message}");
					}
				}
				throw new ConfigException("Unable to connect to tor control port");
			}
			else if(connectResult == TorConnectionSettings.ConnectionTest.Success)
				return NullDisposable.Instance;
			else if(!autoConfig && connectResult == TorConnectionSettings.ConnectionTest.AuthError)
				throw new ConfigException("Unable to authenticate tor control port");

			if(autoConfig)
				AutoDetectCookieFile();
			connectResult = await TryConnectAsync().ConfigureAwait(false);
			if(connectResult != TorConnectionSettings.ConnectionTest.Success)
				throw new ConfigException("Unable to authenticate tor control port");
			return NullDisposable.Instance;
		}
	}
}
