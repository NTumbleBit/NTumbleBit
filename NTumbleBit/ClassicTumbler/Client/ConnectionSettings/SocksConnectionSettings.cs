using Microsoft.Extensions.Logging;
using NTumbleBit.ClassicTumbler.CLI;
using NTumbleBit.Configuration;
using NTumbleBit.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace NTumbleBit.ClassicTumbler.Client.ConnectionSettings
{
	public interface ITorConnectionSettings
	{
		Task<IDisposable> SetupAsync(ClientInteraction interaction, string torPath);
	}
	public class SocksConnectionSettings : ConnectionSettingsBase, ITorConnectionSettings
	{
		public IPEndPoint Proxy
		{
			get; set;
		}

		public async Task<IDisposable> SetupAsync(ClientInteraction interaction, string torPath)
		{
			var autoConfig = Proxy.Address.Equals(IPAddress.Parse("127.0.0.1"));
			if(!await TestConnectionAsync(Proxy).ConfigureAwait(false))
			{
				if(torPath != null && autoConfig)
				{
					var args = $"-socksport {Proxy.Port}";
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
				throw new ConfigException($"Unable to connect to SOCKS {Proxy.Address}:{Proxy.Port}");			
			}
			return NullDisposable.Instance;
		}

		private async Task<bool> TestConnectionAsync(IPEndPoint proxy)
		{
			var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			try
			{

				using(socket)
				{
					await socket.ConnectAsync(proxy).ConfigureAwait(false);
					return true;
				}
			}
			catch { }
			return false;
		}

		public override HttpMessageHandler CreateHttpHandler()
		{
			var handler = new Tor.SocksMessageHandler(Proxy, new TCPServer.Client.ClientOptions() { IncludeHeaders = false });
			return handler;
		}
	}
}
