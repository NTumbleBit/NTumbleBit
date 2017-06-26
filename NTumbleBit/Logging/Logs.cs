using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.Logging
{
	public class Logs
	{
		static Logs()
		{
			Configure(new FuncLoggerFactory(n => NullLogger.Instance));
		}
		public static void Configure(ILoggerFactory factory)
		{
			Configuration = factory.CreateLogger("Configuration");
			Main = factory.CreateLogger("Main");
			Server = factory.CreateLogger("Server");
		}
		public static ILogger Main
		{
			get; set;
		}
		public static ILogger Server
		{
			get; set;
		}
		public static ILogger Configuration
		{
			get; set;
		}
		public const int ColumnLength = 16;
	}
}
