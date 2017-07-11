using DotNetTor.SocksPort;
using NTumbleBit.Configuration;
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

namespace NTumbleBit.ClassicTumbler.Client.ConnectionSettings
{
	public class TorConnectionSettings : ConnectionSettingsBase
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
}
