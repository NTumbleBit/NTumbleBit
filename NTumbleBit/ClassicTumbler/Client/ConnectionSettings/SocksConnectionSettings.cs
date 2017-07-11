using DotNetTor.SocksPort;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;

namespace NTumbleBit.ClassicTumbler.Client.ConnectionSettings
{
	public class SocksConnectionSettings : ConnectionSettingsBase
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
}
