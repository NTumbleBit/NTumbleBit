using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using NBitcoin;
using System.Diagnostics;

namespace NTumbleBit.TumblerServer
{
	public class Startup
	{
		// This method gets called by the runtime. Use this method to add services to the container.
		// For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddSingleton<TumblerConfiguration>((provider) =>
			{
				var factory = provider.GetService<ILoggerFactory>();
				var logger = factory.CreateLogger<TumblerConfiguration>();
				var rsaFile = Path.Combine(Directory.GetCurrentDirectory(), "Tumbler.pem");
				RsaKey rsaKey = null;
				if(!File.Exists(rsaFile))
				{
					logger.LogWarning("RSA private key not found, please backup it. Creating...");
					rsaKey = new RsaKey();
					File.WriteAllBytes(rsaFile, rsaKey.ToBytes());
					logger.LogInformation("RSA key saved (" + rsaFile + ")");
				}
				else
				{
					logger.LogInformation("RSA private key found (" + rsaFile + ")");
					rsaKey = new RsaKey(File.ReadAllBytes(rsaFile));
				}

				ExtKey key = null;
				var seedFile = Path.Combine(Directory.GetCurrentDirectory(), "Seed.dat");
				if(!File.Exists(seedFile))
				{
					logger.LogWarning("Bitcoin seed not found, please note the following backup phrase, it will not be shown twice. Creating...");
					Mnemonic mnemo = new Mnemonic(Wordlist.English);
					logger.LogWarning(mnemo.ToString());
					key = mnemo.DeriveExtKey();
					File.WriteAllBytes(seedFile, key.ToBytes());
					logger.LogInformation("Seed saved (" + seedFile + ")");
				}
				else
				{
					logger.LogInformation("Seed found (" + seedFile + ")");
					key = new ExtKey(File.ReadAllBytes(seedFile));
				}
				Debug.Assert(key != null);
				Debug.Assert(rsaKey != null);
				return new TumblerConfiguration()
				{
					RsaKey = rsaKey,
					Seed = key
				};
			});
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			var logging = new FilterLoggerSettings();
			logging.Add("Microsoft.AspNetCore.Hosting.Internal.WebHost", LogLevel.Error);
			logging.Add("Microsoft.AspNetCore.Mvc", LogLevel.Error);
			loggerFactory
				.WithFilter(logging)
				.AddConsole();

			if(env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseMvc();
		}
	}
}
