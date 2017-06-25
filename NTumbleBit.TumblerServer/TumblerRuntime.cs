using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin.RPC;
using NTumbleBit.Common;
using System.IO;
using NTumbleBit.Common.Logging;
using NTumbleBit.TumblerServer.Services;
using NTumbleBit.ClassicTumbler;
using NBitcoin;

namespace NTumbleBit.TumblerServer
{
    public class TumblerRuntime : IDisposable
    {
		
		public static TumblerRuntime FromConfiguration(TumblerConfiguration conf)
		{
			if(conf == null)
				throw new ArgumentNullException("conf");
			TumblerRuntime runtime = new TumblerRuntime();
			runtime.Cooperative = conf.Cooperative;
			runtime.ClassicTumblerParameters = Serializer.Clone(conf.ClassicTumblerParameters);
			runtime.Network = conf.Network;
			RPCClient rpcClient = null;
			try
			{
				rpcClient = conf.RPC.ConfigureRPCClient(conf.Network);
			}
			catch
			{
				throw new ConfigException("Please, fix rpc settings in " + conf.ConfigurationFile);
			}

			var rsaFile = Path.Combine(conf.DataDir, "Tumbler.pem");
			if(!File.Exists(rsaFile))
			{
				Logs.Configuration.LogWarning("RSA private key not found, please backup it. Creating...");
				runtime.TumblerKey = new RsaKey();
				File.WriteAllBytes(rsaFile, runtime.TumblerKey.ToBytes());
				Logs.Configuration.LogInformation("RSA key saved (" + rsaFile + ")");
			}
			else
			{
				Logs.Configuration.LogInformation("RSA private key found (" + rsaFile + ")");
				runtime.TumblerKey = new RsaKey(File.ReadAllBytes(rsaFile));
			}

			var voucherFile = Path.Combine(conf.DataDir, "Voucher.pem");
			if(!File.Exists(voucherFile))
			{
				Logs.Configuration.LogWarning("Creation of Voucher Key");
				runtime.VoucherKey = new RsaKey();
				File.WriteAllBytes(voucherFile, runtime.VoucherKey.ToBytes());
				Logs.Configuration.LogInformation("RSA key saved (" + voucherFile + ")");
			}
			else
			{
				Logs.Configuration.LogInformation("Voucher key found (" + voucherFile + ")");
				runtime.VoucherKey = new RsaKey(File.ReadAllBytes(voucherFile));
			}

			runtime.ClassicTumblerParameters.ServerKey = runtime.TumblerKey.PubKey;
			runtime.ClassicTumblerParameters.VoucherKey = runtime.VoucherKey.PubKey;

			var dbreeze = new DBreezeRepository(Path.Combine(conf.DataDir, "db2"));
			runtime.Repository = dbreeze;
			runtime._Resources.Add(dbreeze);
			runtime.Tracker = new Tracker(dbreeze);			
			runtime.Services = ExternalServices.CreateFromRPCClient(rpcClient, dbreeze, runtime.Tracker);
			return runtime;
		}

		public void Dispose()
		{
			foreach(var resource in _Resources)
				resource.Dispose();
			_Resources.Clear();
		}

		List<IDisposable> _Resources = new List<IDisposable>();

		public ClassicTumblerParameters ClassicTumblerParameters
		{
			get; set;
		}

		public ExternalServices Services
		{
			get; set;
		}

		public Tracker Tracker
		{
			get; set;
		}

		public bool Cooperative
		{
			get; set;
		}

		public RsaKey TumblerKey
		{
			get;
			set;
		}
		public RsaKey VoucherKey
		{
			get;
			set;
		}
		public IRepository Repository
		{
			get;
			set;
		}
		public Network Network
		{
			get;
			set;
		}

		/// <summary>
		/// Test property, the tumbler does not broadcast the fulfill transaction
		/// </summary>
		public bool NoFulFill
		{
			get;
			set;
		}
	}
}
