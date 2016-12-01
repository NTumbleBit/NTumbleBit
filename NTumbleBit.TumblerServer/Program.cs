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
			var configuration = new TumblerConfiguration();
			var host = new WebHostBuilder()
                .UseKestrel()
				.UseAppConfiguration(configuration)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
