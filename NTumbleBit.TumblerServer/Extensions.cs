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

namespace NTumbleBit.TumblerServer
{
	public static class Extensions
	{
		public static IWebHostBuilder UseAppConfiguration(this IWebHostBuilder builder, TumblerConfiguration configuration)
		{
			builder.ConfigureServices(services =>
			{
				services.AddSingleton<ClassicTumblerParameters>((provider) =>
				{
					var conf = provider.GetRequiredService<TumblerConfiguration>();
					return new ClassicTumblerParameters(conf.TumblerKey.PubKey, conf.VoucherKey.PubKey);
				});
				services.AddSingleton<TumblerConfiguration>((provider) =>
			{
				var conf = configuration ?? new TumblerConfiguration();
				var factory = provider.GetRequiredService<ILoggerFactory>();
				var logger = factory.CreateLogger<TumblerConfiguration>();

				var rsaFile = Path.Combine(Directory.GetCurrentDirectory(), "Tumbler.pem");

				if(conf.TumblerKey == null)
				{
					if(!File.Exists(rsaFile))
					{
						logger.LogWarning("RSA private key not found, please backup it. Creating...");
						conf.TumblerKey = new RsaKey();
						File.WriteAllBytes(rsaFile, conf.TumblerKey.ToBytes());
						logger.LogInformation("RSA key saved (" + rsaFile + ")");
					}
					else
					{
						logger.LogInformation("RSA private key found (" + rsaFile + ")");
						conf.TumblerKey = new RsaKey(File.ReadAllBytes(rsaFile));
					}
				}

				if(conf.Seed == null)
				{
					var seedFile = Path.Combine(Directory.GetCurrentDirectory(), "Seed.dat");
					if(!File.Exists(seedFile))
					{
						logger.LogWarning("Bitcoin seed not found, please note the following backup phrase, it will not be shown twice. Creating...");
						Mnemonic mnemo = new Mnemonic(Wordlist.English);
						logger.LogWarning(mnemo.ToString());
						conf.Seed = mnemo.DeriveExtKey();
						File.WriteAllBytes(seedFile, conf.Seed.ToBytes());
						logger.LogInformation("Seed saved (" + seedFile + ")");
					}
					else
					{
						logger.LogInformation("Seed found (" + seedFile + ")");
						conf.Seed = new ExtKey(File.ReadAllBytes(seedFile));
					}
				}

				if(conf.VoucherKey == null)
				{
					var voucherFile = Path.Combine(Directory.GetCurrentDirectory(), "Voucher.pem");
					if(!File.Exists(rsaFile))
					{
						logger.LogWarning("Creation of Voucher Key");
						conf.VoucherKey = new RsaKey();
						File.WriteAllBytes(rsaFile, conf.VoucherKey.ToBytes());
						logger.LogInformation("RSA key saved (" + voucherFile + ")");
					}
					else
					{
						logger.LogInformation("Voucher key found (" + voucherFile + ")");
						conf.VoucherKey = new RsaKey(File.ReadAllBytes(rsaFile));
					}
				}

				Debug.Assert(conf.Seed != null);
				Debug.Assert(conf.TumblerKey != null);
				Debug.Assert(conf.VoucherKey != null);				

				logger.LogInformation("Testing RPC connection to " + conf.RPCClient.Address.AbsoluteUri);
				try
				{
					conf.RPCClient.SendCommand("whatever");
				}
				catch(RPCException ex)
				{
					if(ex.RPCCode != RPCErrorCode.RPC_METHOD_NOT_FOUND)
					{
						logger.LogError("Error connecting to RPC " + ex.Message);
						throw;
					}
				}
				catch(Exception ex)
				{
					logger.LogError("Error connecting to RPC " + ex.Message);
					throw;
				}
				logger.LogInformation("RPC connection successfull");

				return configuration;
			});
			});
			return builder;
		}
	}
}
