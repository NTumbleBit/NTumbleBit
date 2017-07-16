using Microsoft.AspNetCore.Hosting;
using TCPServer;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using NBitcoin;
using System.Diagnostics;
using NBitcoin.RPC;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.Services;
using Microsoft.AspNetCore.Mvc;
using NTumbleBit.Logging;

namespace NTumbleBit.ClassicTumbler.Server
{
	public class ActionResultException : Exception
	{
		public ActionResultException(IActionResult result)
		{
			if(result == null)
				throw new ArgumentNullException(nameof(result));
			_Result = result;
		}

		private readonly IActionResult _Result;
		public IActionResult Result
		{
			get
			{
				return _Result;
			}
		}
	}
	public static class Extensions
	{
		public static ActionResultException AsException(this IActionResult actionResult)
		{
			return new ActionResultException(actionResult);
		}
		public static IWebHostBuilder UseAppConfiguration(this IWebHostBuilder builder, TumblerRuntime runtime)
		{
			builder.ConfigureServices(services =>
			{
				services.AddSingleton(provider =>
				 {
					 return runtime;
				 });
			});
			builder.UseTCPServer(new ServerOptions(runtime.LocalEndpoint)
			{
				IncludeHeaders = false
			});
			return builder;
		}
	}
}

