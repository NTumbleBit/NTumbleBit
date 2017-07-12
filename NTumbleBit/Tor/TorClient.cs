using DotNetTor;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NTumbleBit.Tor
{
	public class RegisterHiddenServiceResponse
	{
		public string PrivateKey
		{
			get; set;
		}
		public string ServiceID
		{
			get; set;
		}
		public Uri HiddenServiceUri
		{
			get;
			set;
		}
	}

	public class TorException : Exception
	{
		public TorException(string message, string response) : base(BuildMessage(message, response))
		{
			TorResponse = response;
		}

		public string TorResponse
		{
			get; set;
		}

		private static string BuildMessage(string message, string result)
		{
			return message + ":" + Environment.NewLine + result;
		}
	}

	public class TorClient : IDisposable
	{
		private readonly byte[] _authenticationToken;
		private readonly string _cookieFilePath;
		private readonly IPEndPoint _controlEndPoint;
		private Socket _socket;
		public TorClient(string address = "127.0.0.1", int controlPort = 9051, string password = "")
		{
			_controlEndPoint = new IPEndPoint(IPAddress.Parse(address), controlPort);
			if(password == "")
				_authenticationToken = null;
			else
				_authenticationToken = Encoding.UTF8.GetBytes(password);
		}

		public TorClient(string address, int controlPort, FileInfo cookieFile)
		{
			_controlEndPoint = new IPEndPoint(IPAddress.Parse(address), controlPort);
			_cookieFilePath = cookieFile.FullName;
			_authenticationToken = null;
		}

		public Task ConnectAsync()
		{
			_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			return _socket.ConnectAsync(_controlEndPoint);
		}

		const string KeyType = "NEW:RSA1024";
		public async Task<RegisterHiddenServiceResponse> RegisterHiddenServiceAsync(IPEndPoint endpoint, int virtualPort, string privateKey = null, CancellationToken cts = default(CancellationToken))
		{
			privateKey = privateKey ?? KeyType;
			var command = $"ADD_ONION {privateKey} Port={virtualPort},{endpoint.Address}:{endpoint.Port}";
			var result = await SendCommandAsync(command, cts).ConfigureAwait(false);

			var resp = new RegisterHiddenServiceResponse();
			var serviceIdMatch = System.Text.RegularExpressions.Regex.Match(result, "250-ServiceID=([^\r]*)");
			if(serviceIdMatch.Success)
				resp.ServiceID = serviceIdMatch.Groups[1].Value;
			var privateKeyMatch = System.Text.RegularExpressions.Regex.Match(result, "250-PrivateKey=([^\r]*)");
			if(privateKeyMatch.Success)
				resp.PrivateKey = privateKeyMatch.Groups[1].Value;

			if((resp.PrivateKey == null && privateKey == KeyType) ||
				resp.ServiceID == null)
				throw new TorException("Unexpected response when registering hidden service", result);
			resp.HiddenServiceUri = new UriBuilder() { Scheme = "http", Host = resp.ServiceID + ".onion", Port = virtualPort }.Uri;
			return resp;
		}

		public async Task<bool> AuthenticateAsync(CancellationToken ctsToken = default(CancellationToken))
		{
			string authString = "\"\"";
			if(_authenticationToken != null)
			{
				authString = Util.ByteArrayToString(_authenticationToken);
			}
			else if(_cookieFilePath != null && _cookieFilePath != "")
			{
				authString = Util.ByteArrayToString(File.ReadAllBytes(_cookieFilePath));
			}
			var result = await SendCommandAsync($"AUTHENTICATE {authString}", ctsToken).ConfigureAwait(false);
			return result.StartsWith("250 OK", StringComparison.Ordinal);
		}

		public async Task<IPEndPoint[]> GetSocksListenersAsync(CancellationToken ctsToken = default(CancellationToken))
		{
			var result = await SendCommandAsync("GETINFO net/listeners/socks", ctsToken).ConfigureAwait(false);
			return Regex
				.Matches(result, @"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}):(\d{1,5})")
				.OfType<Match>()
				.Select(m => new IPEndPoint(IPAddress.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)))
				.ToArray();
		}

		public async Task<string> SendCommandAsync(string command, CancellationToken ctsToken = default(CancellationToken))
		{
			command = command.Trim();
			if(!command.EndsWith("\r\n", StringComparison.Ordinal))
			{
				command += "\r\n";
			}
			var commandByteArraySegment = new ArraySegment<byte>(Encoding.ASCII.GetBytes(command));
			await _socket.SendAsync(commandByteArraySegment, SocketFlags.None).ConfigureAwait(false);

			var bufferByteArraySegment = new ArraySegment<byte>(new byte[_socket.ReceiveBufferSize]);
			var receivedCount = await _socket.ReceiveAsync(bufferByteArraySegment, SocketFlags.None).ConfigureAwait(false);
			return Encoding.ASCII.GetString(bufferByteArraySegment.Array, 0, receivedCount);
		}

		public void Dispose()
		{
			if(_socket != null)
			{
				try
				{
					if(_socket.Connected)
						try
						{
							_socket.Shutdown(SocketShutdown.Both);
						}
						catch { }
					_socket.Dispose();
				}
				catch(ObjectDisposedException)
				{
					// good, it's already disposed
				}
				_socket = null;
			}
		}
	}

	class Util
	{
		internal static string ByteArrayToString(byte[] ba)
		{
			string hex = BitConverter.ToString(ba);
			return hex.Replace("-", "");
		}
	}
}
