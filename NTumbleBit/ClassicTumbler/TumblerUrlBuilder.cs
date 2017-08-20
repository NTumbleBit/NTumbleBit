using NBitcoin;
using NBitcoin.Payment;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Web.NBitcoin;

namespace NTumbleBit.ClassicTumbler
{
	public class TumblerUrlBuilder
	{
		public TumblerUrlBuilder()
		{

		}
		public TumblerUrlBuilder(Uri uri)
			: this(uri.AbsoluteUri)
		{
			if(uri == null)
				throw new ArgumentNullException("uri");
		}

		public TumblerUrlBuilder(string uri)
		{
			if(uri == null)
				throw new ArgumentNullException("uri");
			if(!uri.StartsWith("ctb:", StringComparison.OrdinalIgnoreCase))
				throw new FormatException("Invalid scheme");
			uri = uri.Remove(0, "ctb:".Length);
			if(uri.StartsWith("//"))
				uri = uri.Remove(0, 2);

			var paramStart = uri.IndexOf('?');
			string address = null;
			if(paramStart == -1)
				address = uri;
			else
			{
				address = uri.Substring(0, paramStart);
			}
			if(address != String.Empty)
			{
				if(address.EndsWith("/", StringComparison.OrdinalIgnoreCase))
					address = address.Substring(0, address.Length - 1);
				Port = 80;
				var split = address.Split(':');
				if(split.Length != 1 && split.Length != 2)
					throw new FormatException("Invalid host");

				if(split.Length == 2)
					Port = int.Parse(split[1]);
				var host = split[0];
				Host = host;
			}
			uri = uri.Remove(0, paramStart + 1);  //+1 to move past '?'

			Dictionary<string, string> parameters;
			try
			{
				parameters = UriHelper.DecodeQueryParameters(uri);
			}
			catch(ArgumentException)
			{
				throw new FormatException("A URI parameter is duplicated");
			}

			if(parameters.ContainsKey("h"))
			{
				ConfigurationHash = new uint160(parameters["h"]);
				parameters.Remove("h");
			}
			else
			{
				throw new FormatException("The configuration hash is missing");
			}

			//var reqParam = parameters.Keys.FirstOrDefault(k => k.StartsWith("req-", StringComparison.OrdinalIgnoreCase));
			//if(reqParam != null)
			//	throw new FormatException("Non compatible required parameter " + reqParam);
		}

		string _Host;
		public string Host
		{
			get
			{
				return _Host;
			}
			set
			{
				if(value != null)
				{
					if(!value.EndsWith(".onion", StringComparison.Ordinal)
						&& !IsIp(value))
						throw new FormatException("Host can only be an onion address or an IP");

				}
				_Host = value;
			}
		}

		private bool IsIp(string value)
		{
			try
			{
				IPAddress.Parse(value);
				return true;
			}
			catch { return false; }
		}

		public int Port
		{
			get;
			set;
		}

		public uint160 ConfigurationHash
		{
			get; set;
		}

		public Uri GetRoutableUri(bool includeConfigurationHash)
		{
			UriBuilder builder = new UriBuilder();
			builder.Scheme = "http";
			builder.Host = Host;
			if(builder.Port != 80)
				builder.Port = Port;
			if(includeConfigurationHash)
			{
				builder.Path = "api/v1/tumblers/" + ConfigurationHash;
			}
			return builder.Uri;
		}

		public bool IsOnion
		{
			get
			{
				return Host.EndsWith(".onion", StringComparison.Ordinal);
			}
		}

		private static void WriteParameters(Dictionary<string, string> parameters, StringBuilder builder)
		{
			bool first = true;
			foreach(var parameter in parameters)
			{
				if(first)
				{
					first = false;
					builder.Append("?");
				}
				else
					builder.Append("&");
				builder.Append(parameter.Key);
				builder.Append("=");
				builder.Append(HttpUtility.UrlEncode(parameter.Value));
			}
		}

		public override string ToString()
		{
			Dictionary<string, string> parameters = new Dictionary<string, string>();
			StringBuilder builder = new StringBuilder();
			builder.Append("ctb://");
			if(Host != null)
			{
				builder.Append(Host.ToString());
			}
			if(Port != 80)
			{
				builder.Append(":" + Port.ToString());
			}
			if(ConfigurationHash != null)
			{
				parameters.Add("h", ConfigurationHash.ToString());
			}

			WriteParameters(parameters, builder);
			return builder.ToString();
		}
	}
}
