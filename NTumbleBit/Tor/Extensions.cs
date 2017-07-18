using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NTumbleBit.Tor
{
    public static class Extensions
    {
		internal static async Task<byte[]> ReadBytes(this Stream stream, int length, CancellationToken cancellation)
		{
			var bytes = new byte[length];
			int readen = 0;
			while(readen != (int)length)
				readen += await stream.ReadAsync(bytes, readen, (int)length - readen, cancellation).ConfigureAwait(false);
			return bytes;
		}
		internal static Task ConnectAsync(this Socket socket, EndPoint endpoint, CancellationToken cancellationToken)
		{
			var args = new SocketAsyncEventArgs();
			CancellationTokenRegistration registration = default(CancellationTokenRegistration);

			TaskCompletionSource<bool> clientSocket = new TaskCompletionSource<bool>();
			Action processClientSocket = () =>
			{
				try
				{
					registration.Dispose();
				}
				catch { }
				if(cancellationToken.IsCancellationRequested)
					clientSocket.TrySetCanceled(cancellationToken);
				else if(args.SocketError != SocketError.Success)
					clientSocket.TrySetException(new SocketException((int)args.SocketError));
				else
					clientSocket.TrySetResult(true);
			};
			args.RemoteEndPoint = endpoint;
			args.Completed += (s, e) => processClientSocket();
			registration = cancellationToken.Register(() =>
			{
				clientSocket.TrySetCanceled(cancellationToken);
				try
				{
					registration.Dispose();
				}
				catch { }
			});
			cancellationToken.Register(() =>
			{
				clientSocket.TrySetCanceled(cancellationToken);
			});
			if(!socket.ConnectAsync(args))
				processClientSocket();
			return clientSocket.Task;
		}
	}
}
