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
using NTumbleBit.Tor;

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
			await runtime.ConfigureAsync(conf, interaction).ConfigureAwait(false);
			return runtime;
		}
		public async Task ConfigureAsync(TumblerConfiguration conf, ClientInteraction interaction)
		{
			try
			{
				await ConfigureAsyncCore(conf, interaction).ConfigureAwait(false);
			}
			catch
			{
				Dispose();
				throw;
			}
		}
		async Task ConfigureAsyncCore(TumblerConfiguration conf, ClientInteraction interaction)
		{
			Cooperative = conf.Cooperative;
			ClassicTumblerParameters = conf.ClassicTumblerParameters.Clone();
			Network = conf.Network;
			LocalEndpoint = conf.Listen;
			
			bool torConfigured = false;
			if(conf.TorSettings != null)
			{
				Exception error = null;
				try
				{
					_Resources.Add(await conf.TorSettings.SetupAsync(interaction, conf.TorPath).ConfigureAwait(false));
					Logs.Configuration.LogInformation("Successfully authenticated to Tor");
					var torRSA = Path.Combine(conf.DataDir, "Tor.rsa");


					string privateKey = null;
					if(File.Exists(torRSA))
						privateKey = File.ReadAllText(torRSA, Encoding.UTF8);

					TorConnection = conf.TorSettings.CreateTorClient2();
					_Resources.Add(TorConnection);

					await TorConnection.ConnectAsync().ConfigureAwait(false);
					await TorConnection.AuthenticateAsync().ConfigureAwait(false);
					var result = await TorConnection.RegisterHiddenServiceAsync(conf.Listen, conf.TorSettings.VirtualPort, privateKey).ConfigureAwait(false);
					if(privateKey == null)
					{
						File.WriteAllText(torRSA, result.PrivateKey, Encoding.UTF8);
						Logs.Configuration.LogWarning($"Tor RSA private key generated to {torRSA}");
					}

					var tumblerUri = new TumblerUrlBuilder();
					tumblerUri.Port = result.HiddenServiceUri.Port;
					tumblerUri.Host = result.HiddenServiceUri.Host;
					TumblerUris.Add(tumblerUri);
					TorUri = tumblerUri.RoutableUri;
					Logs.Configuration.LogInformation($"Tor configured on {result.HiddenServiceUri}");
					torConfigured = true;
				}
				catch(ConfigException ex)
				{
					error = ex;
				}
				catch(TorException ex)
				{
					error = ex;
				}
				catch(ClientInteractionException)
				{
				}
				if(error != null)
					Logs.Configuration.LogWarning("Error while configuring Tor hidden service: " + error.Message);
			}

			if(!torConfigured)
				Logs.Configuration.LogWarning("The tumbler is not configured as a Tor Hidden service");

			var rsaFile = Path.Combine(conf.DataDir, "Tumbler.pem");
			if(!File.Exists(rsaFile))
			{
				Logs.Configuration.LogWarning("RSA private key not found, please backup it. Creating...");
				TumblerKey = new RsaKey();
				File.WriteAllBytes(rsaFile, TumblerKey.ToBytes());
				Logs.Configuration.LogInformation("RSA key saved (" + rsaFile + ")");
			}
			else
			{
				Logs.Configuration.LogInformation("RSA private key found (" + rsaFile + ")");
				TumblerKey = new RsaKey(File.ReadAllBytes(rsaFile));
			}

			var voucherFile = Path.Combine(conf.DataDir, "Voucher.pem");
			if(!File.Exists(voucherFile))
			{
				Logs.Configuration.LogWarning("Creation of Voucher Key");
				VoucherKey = new RsaKey();
				File.WriteAllBytes(voucherFile, VoucherKey.ToBytes());
				Logs.Configuration.LogInformation("RSA key saved (" + voucherFile + ")");
			}
			else
			{
				Logs.Configuration.LogInformation("Voucher key found (" + voucherFile + ")");
				VoucherKey = new RsaKey(File.ReadAllBytes(voucherFile));
			}

			ClassicTumblerParameters.ServerKey = TumblerKey.PubKey;
			ClassicTumblerParameters.VoucherKey = VoucherKey.PubKey;
			ClassicTumblerParametersHash = ClassicTumblerParameters.GetHash();

			if(conf.AllowInsecure)
			{
				TumblerUris.Add(new TumblerUrlBuilder()
				{
					Host = LocalEndpoint.Address.ToString(),
					Port = LocalEndpoint.Port,
				});
			}

			var configurationHash = ClassicTumblerParameters.GetHash();
			foreach(var uri in TumblerUris)
			{
				uri.ConfigurationHash = configurationHash;
			}

			Logs.Configuration.LogInformation("");
			Logs.Configuration.LogInformation($"--------------------------------");
			Logs.Configuration.LogInformation($"Shareable URIs of the running tumbler are:");
			foreach(var uri in TumblerUris)
			{
				Logs.Configuration.LogInformation(uri.ToString());
			}
			Logs.Configuration.LogInformation($"--------------------------------");
			Logs.Configuration.LogInformation("");
			
			Repository = conf.DBreezeRepository;
			_Resources.Add(Repository);
			Tracker = conf.Tracker;
			Services = conf.Services;
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

		public IExternalServices Services
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
		public DBreezeRepository Repository
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
		public List<TumblerUrlBuilder> TumblerUris
		{
			get;
			set;
		} = new List<TumblerUrlBuilder>();
		public TorClient TorConnection
		{
			get;
			private set;
		}
		public IPEndPoint LocalEndpoint
		{
			get;
			set;
		}		
	}
}
