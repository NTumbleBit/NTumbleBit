using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;

namespace NTumbleBit.ClassicTumbler.Client.ConnectionSettings
{
	public class HttpConnectionSettings : ConnectionSettingsBase
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
}
