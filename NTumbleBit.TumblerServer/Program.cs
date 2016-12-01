using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace NTumbleBit.TumblerServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
			var options = new ConfigurationBuilder();
			options.AddCommandLine(args);
			var host = new WebHostBuilder()
                .UseKestrel()
				.UseConfiguration(options.Build())
				.UseHostingConfiguration(options)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
