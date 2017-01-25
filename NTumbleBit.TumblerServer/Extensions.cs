using Microsoft.AspNetCore.Hosting;
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
using NTumbleBit.TumblerServer.Services;
using NTumbleBit.TumblerServer.Services.RPCServices;
using Microsoft.AspNetCore.Mvc;
using NTumbleBit.Common;
using NTumbleBit.Common.Logging;

namespace NTumbleBit.TumblerServer
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
		public static IWebHostBuilder UseAppConfiguration(this IWebHostBuilder builder, TumblerConfiguration configuration)
		{
			builder.ConfigureServices(services =>
			{
				services.AddSingleton(provider =>
				 {
					 var conf = provider.GetRequiredService<TumblerConfiguration>();
					 var repo = provider.GetRequiredService<IRepository>();
					 return new ClassicTumblerRepository(conf, repo);
				 });

				services.AddSingleton<IRepository>(provider =>
				{
					var conf = provider.GetRequiredService<TumblerConfiguration>();
					var dbreeze = new DBreezeRepository(Path.Combine(conf.DataDir, "db"));
					return dbreeze;
				});

				services.AddSingleton((provider) =>
				{
					var conf = provider.GetRequiredService<TumblerConfiguration>();
					var repo = provider.GetRequiredService<IRepository>();
					return ExternalServices.CreateFromRPCClient(conf.RPCClient, repo);
				});
				services.AddSingleton((provider) =>
				{
					var conf = provider.GetRequiredService<TumblerConfiguration>();
					return conf.CreateClassicTumblerParameters();
				});
				services.AddSingleton((provider) =>
				{
					var conf = configuration ?? new TumblerConfiguration().LoadArgs(new string[0]);

					var rsaFile = Path.Combine(conf.DataDir, "Tumbler.pem");

					if(conf.TumblerKey == null)
					{
						if(!File.Exists(rsaFile))
						{
							Logs.Configuration.LogWarning("RSA private key not found, please backup it. Creating...");
							conf.TumblerKey = new RsaKey();
							File.WriteAllBytes(rsaFile, conf.TumblerKey.ToBytes());
							Logs.Configuration.LogInformation("RSA key saved (" + rsaFile + ")");
						}
						else
						{
							Logs.Configuration.LogInformation("RSA private key found (" + rsaFile + ")");
							conf.TumblerKey = new RsaKey(File.ReadAllBytes(rsaFile));
						}
					}

					if(conf.VoucherKey == null)
					{
						var voucherFile = Path.Combine(conf.DataDir, "Voucher.pem");
						if(!File.Exists(voucherFile))
						{
							Logs.Configuration.LogWarning("Creation of Voucher Key");
							conf.VoucherKey = new RsaKey();
							File.WriteAllBytes(voucherFile, conf.VoucherKey.ToBytes());
							Logs.Configuration.LogInformation("RSA key saved (" + voucherFile + ")");
						}
						else
						{
							Logs.Configuration.LogInformation("Voucher key found (" + voucherFile + ")");
							conf.VoucherKey = new RsaKey(File.ReadAllBytes(voucherFile));
						}
					}

					Debug.Assert(conf.TumblerKey != null);
					Debug.Assert(conf.VoucherKey != null);

					try
					{

						conf.RPCClient = conf.RPCClient ?? conf.RPC.ConfigureRPCClient(conf.Network);
					}
					catch
					{
						throw new ConfigException("Please, fix rpc settings in " + conf.ConfigurationFile);
					}
					return configuration;
				});
			});

			builder.UseUrls(configuration.GetUrls());

			return builder;
		}
	}
}

