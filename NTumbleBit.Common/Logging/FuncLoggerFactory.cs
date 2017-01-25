using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.Common.Logging
{
	public class FuncLoggerFactory : ILoggerFactory
	{
		private Func<string, ILogger> createLogger;
		public FuncLoggerFactory(Func<string, ILogger> createLogger)
		{
			this.createLogger = createLogger;
		}
		public void AddProvider(ILoggerProvider provider)
		{
			
		}

		public ILogger CreateLogger(string categoryName)
		{
			return createLogger(categoryName);
		}

		public void Dispose()
		{
			
		}
	}
}
