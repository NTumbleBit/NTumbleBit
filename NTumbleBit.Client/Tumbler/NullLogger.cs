using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.Client.Tumbler
{
	public class NullLogger : ILogger
	{
		class NullDisposable : IDisposable
		{
			public void Dispose()
			{
				
			}
		}
		public IDisposable BeginScope<TState>(TState state)
		{
			return new NullDisposable();
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return false;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
		{
		}
	}
}
