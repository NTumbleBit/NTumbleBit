using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace NTumbleBit.TumblerServer
{
    public static class Extensions
    {
		public static IWebHostBuilder UseHostingConfiguration(this IWebHostBuilder builder, ConfigurationBuilder configuration)
		{
			builder.UseConfiguration(configuration.Build());
			builder.ConfigureServices(services =>
			{
				services.AddSingleton<ConfigurationBuilder>(configuration);
			});
			return builder;
		}
    }
}
