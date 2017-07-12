using NTumbleBit.Configuration;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;

namespace NTumbleBit.ClassicTumbler.Client.ConnectionSettings
{
	public class ConnectionSettingsBase
	{
		public static ConnectionSettingsBase ParseConnectionSettings(string prefix, TextFileConfiguration config, string defaultType = "tor")
		{
			var type = config.GetOrDefault<string>(prefix + ".proxy.type", defaultType);
			if(type.Equals("none", StringComparison.OrdinalIgnoreCase))
			{
				return new ConnectionSettingsBase();
			}
			else if(type.Equals("tor", StringComparison.OrdinalIgnoreCase))
			{
				return TorConnectionSettings.ParseConnectionSettings(prefix + ".proxy", config);
			}
			else
				throw new ConfigException(prefix + ".proxy.type is not supported, should be tor");
		}
		public virtual HttpMessageHandler CreateHttpHandler()
		{
			return null;
		}
	}
}
