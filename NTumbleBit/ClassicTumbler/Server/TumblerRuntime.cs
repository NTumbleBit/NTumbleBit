using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin.RPC;
using System.IO;
using NTumbleBit.Logging;
using NTumbleBit.Services;
using NTumbleBit.ClassicTumbler;
using NBitcoin;
using NTumbleBit.Configuration;
using NTumbleBit.ClassicTumbler.Client;
using NTumbleBit.ClassicTumbler.CLI;
using System.Text;
using System.Net;
using System.Threading;

namespace NTumbleBit.ClassicTumbler.Server
{
	public class TumblerRuntime : IDisposable
	{
		public static TumblerRuntime FromConfiguration(TumblerConfiguration conf, ClientInteraction interaction)
		{
			return FromConfigurationAsync(conf, interaction).GetAwaiter().GetResult();
		}
		public static async Task<TumblerRuntime> FromConfigurationAsync(TumblerConfiguration conf, ClientInteraction interaction)
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

			bool torConfigured = false;
			if(conf.TorSettings != null)
			{
				try
				{
					await conf.TorSettings.SetupAsync(interaction).ConfigureAwait(false);
					Logs.Configuration.LogInformation("Successfully authenticated to Tor");
					var torRSA = Path.Combine(conf.DataDir, "Tor.rsa");

					var keyType = "NEW:RSA1024";
					var privateKey = keyType;
					if(File.Exists(torRSA))
					{
						privateKey = File.ReadAllText(torRSA, Encoding.UTF8);
					}
					else
						Logs.Configuration.LogWarning("Tor RSA private key not found, please backup it. Creating...");

					IPEndPoint routable = GetLocalEndpoint(conf);
					var command = $"ADD_ONION {privateKey} Port={conf.TorSettings.VirtualPort},{routable.Address}:{routable.Port}";
					runtime.TorConnection = conf.TorSettings.CreateTorClient2();
					runtime._Resources.Add(runtime.TorConnection);

					await runtime.TorConnection.ConnectAsync().ConfigureAwait(false);
					await runtime.TorConnection.AuthenticateAsync().ConfigureAwait(false);
					var result = await runtime.TorConnection.SendCommandAsync(command).ConfigureAwait(false);

					if(privateKey == keyType)
					{
						privateKey = System.Text.RegularExpressions.Regex.Match(result, "250-PrivateKey=([^\r]*)").Groups[1].Value;
						File.WriteAllText(torRSA, privateKey);
					}
					var serviceId = System.Text.RegularExpressions.Regex.Match(result, "250-ServiceID=([^\r]*)").Groups[1].Value;
					runtime.TorUri = new UriBuilder() { Scheme = "http", Host = serviceId + ".onion", Port = conf.TorSettings.VirtualPort }.Uri;
					Logs.Configuration.LogInformation($"Tor configured on {runtime.TorUri.AbsoluteUri}");

					torConfigured = true;
				}
				catch(ConfigException ex)
				{
					Logs.Configuration.LogWarning("Error while configuring Tor hidden service: " + ex.Message);
				}
				catch(ClientInteractionException)
				{
				}
			}

			if(!torConfigured)
				Logs.Configuration.LogWarning("Tor is turned off");

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
			runtime.ClassicTumblerParametersHash = runtime.ClassicTumblerParameters.GetHash();

			if(runtime.TorUri != null)
				runtime.TumblerUris.Add(runtime.CreateTumblerUri(runtime.TorUri));

			foreach(var url in conf.GetUrls())
				runtime.TumblerUris.Add(runtime.CreateTumblerUri(new Uri(url, UriKind.Absolute)));


			Logs.Configuration.LogInformation("");
			Logs.Configuration.LogInformation($"--------------------------------");
			var uris = String.Join(Environment.NewLine, runtime.TumblerUris.ToArray().Select(u => u.AbsoluteUri).ToArray());
			Logs.Configuration.LogInformation($"Shareable URIs of the running tumbler are:");
			foreach(var uri in runtime.TumblerUris)
			{
				Logs.Configuration.LogInformation(uri.AbsoluteUri);
			}
			Logs.Configuration.LogInformation($"--------------------------------");
			Logs.Configuration.LogInformation("");

			var dbreeze = new DBreezeRepository(Path.Combine(conf.DataDir, "db2"));
			runtime.Repository = dbreeze;
			runtime._Resources.Add(dbreeze);
			runtime.Tracker = new Tracker(dbreeze, runtime.Network);
			runtime.Services = ExternalServices.CreateFromRPCClient(rpcClient, dbreeze, runtime.Tracker);
			return runtime;
		}

		private static IPEndPoint GetLocalEndpoint(TumblerConfiguration conf)
		{
			var routable = conf.Listen.FirstOrDefault();
			if(routable.Address == IPAddress.Any)
				routable = new IPEndPoint(IPAddress.Parse("127.0.0.1"), routable.Port);
			return routable;
		}

		public Uri TorUri
		{
			get; set;
		}

		public void Dispose()
		{
			//TODO: This is called by both the webhost and the interactive console
			//      but webhost should not take care of cleaning up
			lock(_Resources)
			{
				foreach(var resource in _Resources)
					resource.Dispose();
				_Resources.Clear();
			}
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
		public uint160 ClassicTumblerParametersHash
		{
			get;
			internal set;
		}
		public List<Uri> TumblerUris
		{
			get;
			set;
		} = new List<Uri>();
		public TorClient TorConnection
		{
			get;
			private set;
		}

		private Uri CreateTumblerUri(Uri baseUri)
		{
			var builder = new UriBuilder(baseUri);
			builder.Path = $"/api/v1/tumblers/{ClassicTumblerParametersHash}";
			return builder.Uri;
		}
	}
}
