using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCPServer.Client;

namespace NTumbleBit.Tor
{
	public enum SocksErrorCode
	{
		Success = 0,
		GeneralServerFailure = 1,
		ConnectionNotAllowed = 2,
		NetworkUnreachable = 3,
		HostUnreachable = 4,
		ConnectionRefused = 5,
		TTLExpired = 6,
		CommandNotSupported = 7,
		AddressTypeNotSupported = 8,
	}
	public class SocksException : Exception
	{
		public SocksException(SocksErrorCode errorCode) : base(GetMessageForCode((int)errorCode))
		{
			SocksErrorCode = errorCode;
		}

		public SocksErrorCode SocksErrorCode
		{
			get; set;
		}

		private static string GetMessageForCode(int errorCode)
		{
			switch(errorCode)
			{
				case 0:
					return "Success";
				case 1:
					return "general SOCKS server failure";
				case 2:
					return "connection not allowed by ruleset";
				case 3:
					return "Network unreachable";
				case 4:
					return "Host unreachable";
				case 5:
					return "Connection refused";
				case 6:
					return "TTL expired";
				case 7:
					return "Command not supported";
				case 8:
					return "Address type not supported";
				default:
					return "Unknown code";
			}
		}

		public SocksException(string message) : base(message)
		{

		}
	}
	public class SocksMessageHandler : TCPServer.Client.TCPHttpMessageHandler
	{
		static readonly byte[] SelectionMessage = new byte[] { 5, 1, 0 };
		IPEndPoint _SocksEndpoint;
		public SocksMessageHandler(IPEndPoint socksEndpoint, ClientOptions options = null) : base(options)
		{
			if(socksEndpoint == null)
				throw new ArgumentNullException(nameof(socksEndpoint));
			_SocksEndpoint = socksEndpoint;
		}

		protected override async Task<Socket> CreateSocket(ConnectionEndpoint endpoint, CancellationToken cancellation)
		{
			Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			await s.ConnectAsync(_SocksEndpoint, cancellation).ConfigureAwait(false);
			NetworkStream stream = new NetworkStream(s, false);

			await stream.WriteAsync(SelectionMessage, 0, SelectionMessage.Length, cancellation).ConfigureAwait(false);
			await stream.FlushAsync(cancellation).ConfigureAwait(false);

			var selectionResponse = await stream.ReadBytes(2, cancellation);
			if(selectionResponse[0] != 5)
				throw new SocksException("Invalid version in selection reply");
			if(selectionResponse[1] != 0)
				throw new SocksException("Unsupported authentication method in selection reply");

			var connectBytes = CreateConnectMessage(endpoint.Host, endpoint.Port);
			await stream.WriteAsync(connectBytes, 0, connectBytes.Length, cancellation).ConfigureAwait(false);
			await stream.FlushAsync(cancellation).ConfigureAwait(false);

			var connectResponse = await stream.ReadBytes(10, cancellation);
			if(connectResponse[0] != 5)
				throw new SocksException("Invalid version in connect reply");
			if(connectResponse[1] != 0)
				throw new SocksException((SocksErrorCode)connectResponse[1]);
			if(connectResponse[2] != 0)
				throw new SocksException("Invalid RSV in connect reply");
			if(connectResponse[3] != 1)
				throw new SocksException("Invalid ATYP in connect reply");
			for(int i = 4; i < 4 + 4; i++)
			{
				if(connectResponse[i] != 0)
					throw new SocksException("Invalid BIND address in connect reply");
			}

			if(connectResponse[8] != 0 || connectResponse[9] != 0)
				throw new SocksException("Invalid PORT address connect reply");

			return s;
		}

		protected override ConnectionEndpoint GetEndpoint(Uri request)
		{
			if(!request.DnsSafeHost.EndsWith(".onion"))
				throw new NotSupportedException("SocksMessageHandler only support onion address");
			return base.GetEndpoint(request);
		}

		//From DotNetTor
		internal static byte[] CreateConnectMessage(string host, int port)
		{
			byte[] sendBuffer;
			byte[] nameBytes = Encoding.ASCII.GetBytes(host);

			var addressBytes =
				Enumerable.Empty<byte>()
				.Concat(new[] { (byte)nameBytes.Length })
				.Concat(nameBytes).ToArray();

			sendBuffer =
					Enumerable.Empty<byte>()
					.Concat(
						new byte[]
						{
							(byte)5, (byte) 0x01, (byte) 0x00, (byte)0x03
						})
						.Concat(addressBytes)
						.Concat(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)port))).ToArray();
			return sendBuffer;
		}

	}
}
