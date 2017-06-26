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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace NTumbleBit.ClassicTumbler.Server
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
			logging.Add("Microsoft.AspNetCore.Server.Kestrel", LogLevel.Error);
			loggerFactory
				.WithFilter(logging)
				.AddConsole();

			app.UseMvc();


			var builder = serviceProvider.GetService<ConfigurationBuilder>() ?? new ConfigurationBuilder();
			Configuration = builder.Build();

			var config = serviceProvider.GetService<TumblerRuntime>();
			var options = GetMVCOptions(serviceProvider);
			Serializer.RegisterFrontConverters(options.SerializerSettings, config.Network);
		}

		public IConfiguration Configuration
		{
			get; set;
		}

		private static MvcJsonOptions GetMVCOptions(IServiceProvider serviceProvider)
		{
			return serviceProvider.GetRequiredService<IOptions<MvcJsonOptions>>().Value;
		}
	}

	internal class NoObjectModelValidator : IObjectModelValidator
	{
		public void Validate(ActionContext actionContext, ValidationStateDictionary validationState, string prefix, object model)
		{

		}
	}
}
