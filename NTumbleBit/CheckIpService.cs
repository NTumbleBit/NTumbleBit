using NTumbleBit.ClassicTumbler.Client;
using Microsoft.Extensions.Logging;
using NTumbleBit.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class CheckIpService : TumblerServiceBase
	{
		private TumblerClientRuntime runtime;

		public CheckIpService(TumblerClientRuntime runtime)
		{
			this.runtime = runtime;
		}
		public override string Name => "checkip";
		TimeSpan IpDuration = TimeSpan.FromMinutes(10);
		protected override void StartCore(CancellationToken cancellationToken)
		{
			new Thread(() =>
			{
				var lastChangeAddress = DateTimeOffset.UtcNow;
				IPAddress lastIp = null;
				try
				{
					while(true)
					{
						var ip = GetExternalIp(runtime.CreateTumblerClient(0, Identity.Alice), "https://myexternalip.com/raw").GetAwaiter().GetResult();
						var ipLife = (DateTimeOffset.UtcNow - lastChangeAddress);

						if(lastIp == null)
							lastIp = ip;
						else if(lastIp.Equals(ip))
						{
							if(ipLife > IpDuration)
								Logs.Client.LogWarning($"Address should have changed {(ipLife - IpDuration).ToString("hh\\:mm\\:ss")} ago");
						}
						else
						{
							Logs.Client.LogInformation($"Address change detected after {ipLife.ToString("hh\\:mm\\:ss")}");
							lastChangeAddress = DateTimeOffset.UtcNow;
							lastIp = ip;
						}
						cancellationToken.WaitHandle.WaitOne(60000);
						cancellationToken.ThrowIfCancellationRequested();
					}
				}
				catch(OperationCanceledException) { }
				Stopped();
			}).Start();
		}

		internal static async Task<IPAddress> GetExternalIp(TumblerClient client, string url)
		{
			var result = await client.Client.GetAsync(url).ConfigureAwait(false);
			var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
			return IPAddress.Parse(content.Replace("\n", string.Empty));
		}
	}
}
