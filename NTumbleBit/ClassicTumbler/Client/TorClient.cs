using DotNetTor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler.Client
{
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

		public async Task AuthenticateAsync(CancellationToken ctsToken = default(CancellationToken))
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
			await SendCommandAsync($"AUTHENTICATE {authString}", ctsToken).ConfigureAwait(false);
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
