using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.Common.Logging
{
	public class Logs
	{
		static Logs()
		{
			Configure(new FuncLoggerFactory(n => new NullLogger()));
		}
		public static void Configure(ILoggerFactory factory)
		{
			Configuration = factory.CreateLogger("Configuration");
			Main = factory.CreateLogger("Main");
		}
		public static ILogger Main
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
