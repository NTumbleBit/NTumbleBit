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
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NBitcoin.RPC;
using NTumbleBit.ClassicTumbler;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace NTumbleBit.TumblerServer
{
	public class Startup
	{
		// This method gets called by the runtime. Use this method to add services to the container.
		// For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddSingleton<IObjectModelValidator, NoObjectModelValidator>();
			services.AddMvcCore(o => o.Filters.Add(new ActionResultExceptionFilter()))
				.AddJsonFormatters()
				.AddFormatterMappings();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env,
			ILoggerFactory loggerFactory,
			IServiceProvider serviceProvider)
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

			var builder = serviceProvider.GetService<ConfigurationBuilder>() ?? new ConfigurationBuilder();
			builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
			builder.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);
			Configuration = builder.Build();

			app.UseMvc();
			app.UseDefaultFiles();
			app.UseStaticFiles(new StaticFileOptions()
			{
				OnPrepareResponse = (context) =>
				{
					context.Context.Response.Headers["Cache-Control"] = Configuration["StaticFiles:Headers:Cache-Control"];
					context.Context.Response.Headers["Pragma"] = Configuration["StaticFiles:Headers:Pragma"];
					context.Context.Response.Headers["Expires"] = Configuration["StaticFiles:Headers:Expires"];
				}
			});

			

			var config = serviceProvider.GetService<TumblerConfiguration>();
			var options = GetMVCOptions(serviceProvider);
			Serializer.RegisterFrontConverters(options.SerializerSettings, config.Network);
		}

		public IConfiguration Configuration
		{
			get; set;
		}

		private static MvcJsonOptions GetMVCOptions(IServiceProvider serviceProvider)
		{
			return serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MvcJsonOptions>>().Value;
		}
	}

	class NoObjectModelValidator : IObjectModelValidator
	{
		public void Validate(ActionContext actionContext, ValidationStateDictionary validationState, string prefix, object model)
		{

		}
	}
}
