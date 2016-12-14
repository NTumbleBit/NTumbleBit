using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.TumblerServer
{
    public class TextFileConfiguration
    {
		public static Dictionary<string, string> Parse(string data)
		{
			Dictionary<string, string> result = new Dictionary<string, string>();
			var lines = data.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
			int lineCount = -1;
			foreach(var l in lines)
			{
				lineCount++;
				var line = l.Trim();
				if(line.StartsWith("#"))
					continue;
				var split = line.Split('=');
				if(split.Length == 0)
					continue;
				if(split.Length == 1)
					throw new FormatException("Line " + lineCount + ": No value are set");

				var key = split[0];
				if(result.ContainsKey(key))
					throw new FormatException("Line " + lineCount + ": Duplicate key " + key);
				var value = String.Join("=", split.Skip(1).ToArray());
				result.Add(key, value);
			}
			return result;
		}

		public static String CreateDefaultConfiguration(Network network)
		{
			StringBuilder builder = new StringBuilder();
			builder.AppendLine("#rpc.url=http://localhost:"+network.RPCPort+ "/");
			builder.AppendLine("#rpc.user=bitcoinuser");
			builder.AppendLine("#rpc.password=bitcoinpassword");
			builder.AppendLine("#network=main (accepted value: main, test, regtest)");
			return builder.ToString();
		}
    }
}
