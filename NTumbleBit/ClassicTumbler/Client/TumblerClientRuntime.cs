using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using System.IO;
using NBitcoin.RPC;
using NTumbleBit.Logging;
using Microsoft.Extensions.Logging;
using NTumbleBit.ClassicTumbler;
using System.Net;
using System.Threading.Tasks;
using System.Net.Http;
using NTumbleBit.Services;
using NTumbleBit.Configuration;
using NTumbleBit.ClassicTumbler.Client.ConnectionSettings;
using NTumbleBit.ClassicTumbler.CLI;

namespace NTumbleBit.ClassicTumbler.Client
{
	public enum Identity
	{
		Alice,
		Bob
	}

	public class TumblerClientRuntime : IDisposable
	{
		public static TumblerClientRuntime FromConfiguration(TumblerClientConfiguration configuration, ClientInteraction interaction)
		{
			return FromConfigurationAsync(configuration, interaction).GetAwaiter().GetResult();
		}

		public static async Task<TumblerClientRuntime> FromConfigurationAsync(TumblerClientConfiguration configuration, ClientInteraction interaction)
		{
			TumblerClientRuntime runtime = new TumblerClientRuntime();
			try
			{
				await runtime.ConfigureAsync(configuration, interaction).ConfigureAwait(false);
			}
			catch
			{
				runtime.Dispose();
                throw;
			}
			return runtime;
		}
		public async Task ConfigureAsync(TumblerClientConfiguration configuration, ClientInteraction interaction)
		{
			interaction = interaction ?? new AcceptAllClientInteraction();

			Network = configuration.Network;
			TumblerServer = configuration.TumblerServer;
			BobSettings = configuration.BobConnectionSettings;
			AliceSettings = configuration.AliceConnectionSettings;

			var torOnly = AliceSettings is TorConnectionSettings && BobSettings is TorConnectionSettings;

			await SetupTorAsync(interaction).ConfigureAwait(false);
			if(torOnly)
				Logs.Configuration.LogInformation("Successfully authenticated to Tor");

			RPCClient rpc = null;
			try
			{
				rpc = configuration.RPCArgs.ConfigureRPCClient(configuration.Network);
			}
			catch
			{
				throw new ConfigException("Please, fix rpc settings in " + configuration.ConfigurationFile);
			}

			var dbreeze = new DBreezeRepository(Path.Combine(configuration.DataDir, "db2"));
			Cooperative = configuration.Cooperative;
			Repository = dbreeze;
			_Disposables.Add(dbreeze);
			Tracker = new Tracker(dbreeze, Network);
			Services = ExternalServices.CreateFromRPCClient(rpc, dbreeze, Tracker);

			if(configuration.OutputWallet.RootKey != null && configuration.OutputWallet.KeyPath != null)
				DestinationWallet = new ClientDestinationWallet(configuration.OutputWallet.RootKey, configuration.OutputWallet.KeyPath, dbreeze, configuration.Network);
			else if(configuration.OutputWallet.RPCArgs != null)
			{
				try
				{
					DestinationWallet = new RPCDestinationWallet(configuration.OutputWallet.RPCArgs.ConfigureRPCClient(Network));
				}
				catch
				{
					throw new ConfigException("Please, fix outputwallet rpc settings in " + configuration.ConfigurationFile);
				}
			}
			else
				throw new ConfigException("Missing configuration for outputwallet");

			TumblerParameters = dbreeze.Get<ClassicTumbler.ClassicTumblerParameters>("Configuration", configuration.TumblerServer.AbsoluteUri);
			var parameterHash = ClassicTumbler.ClassicTumblerParameters.ExtractHashFromUrl(configuration.TumblerServer);

			if(TumblerParameters != null && TumblerParameters.GetHash() != parameterHash)
				TumblerParameters = null;

			if(!configuration.OnlyMonitor)
			{
				if(!torOnly && configuration.CheckIp)
				{
					var ip1 = GetExternalIp(CreateTumblerClient(0, Identity.Alice), "https://myexternalip.com/raw");
					var ip2 = GetExternalIp(CreateTumblerClient(0, Identity.Bob), "https://icanhazip.com/");
					var aliceIp = ip1.GetAwaiter().GetResult();
					var bobIp = ip2.GetAwaiter().GetResult();
					if(aliceIp.Equals(bobIp))
					{
						throw new ConfigException("Same IP detected for Bob and Alice, the tumbler can link input address to output address");
					}
					else
						Logs.Configuration.LogInformation("Alice and Bob have different IP configured");
				}


				var client = CreateTumblerClient(0);
				if(TumblerParameters == null)
				{
					Logs.Configuration.LogInformation("Downloading tumbler information of " + configuration.TumblerServer.AbsoluteUri);
					var parameters = Retry(3, () => client.GetTumblerParameters());
					if(parameters == null)
						throw new ConfigException("Unable to download tumbler's parameters");

					await interaction.ConfirmParametersAsync(parameters).ConfigureAwait(false);
					Repository.UpdateOrInsert("Configuration", TumblerServer.AbsoluteUri, parameters, (o, n) => n);
					TumblerParameters = parameters;

					if(parameters.GetHash() != parameterHash)
						throw new ConfigException("The tumbler returned an invalid configuration");

					Logs.Configuration.LogInformation("Tumbler parameters saved");
				}

				Logs.Configuration.LogInformation($"Using tumbler {TumblerServer.AbsoluteUri}");
			}
		}

		private async Task SetupTorAsync(ClientInteraction interaction)
		{
			await SetupTorAsync(interaction, AliceSettings).ConfigureAwait(false);
			await SetupTorAsync(interaction, BobSettings).ConfigureAwait(false);
		}

		private Task SetupTorAsync(ClientInteraction interaction, ConnectionSettingsBase settings)
		{
			var tor = settings as TorConnectionSettings;
			if(tor == null)
				return Task.CompletedTask;
			return tor.SetupAsync(interaction);
		}

		private static async Task<IPAddress> GetExternalIp(TumblerClient client, string url)
		{
			var result = await client.Client.GetAsync(url).ConfigureAwait(false);
			var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
			return IPAddress.Parse(content.Replace("\n", string.Empty));
		}

		public BroadcasterJob CreateBroadcasterJob()
		{
			return new BroadcasterJob(Services);
		}

		public ConnectionSettingsBase BobSettings
		{
			get; set;
		}

		public ConnectionSettingsBase AliceSettings
		{
			get; set;
		}

		public bool Cooperative
		{
			get; set;
		}

		public Uri TumblerServer
		{
			get; set;
		}

		public TumblerClient CreateTumblerClient(int cycle, Identity? identity = null)
		{
			if(identity == null)
				identity = RandomUtils.GetUInt32() % 2 == 0 ? Identity.Alice : Identity.Bob;
			return CreateTumblerClient(cycle, identity == Identity.Alice ? AliceSettings : BobSettings);
		}

		private TumblerClient CreateTumblerClient(int cycleId, ConnectionSettingsBase settings)
		{
			var client = new TumblerClient(Network, TumblerServer, cycleId);
			var handler = settings.CreateHttpHandler();
			if(handler != null)
				client.SetHttpHandler(handler);
			return client;
		}

		public StateMachinesExecutor CreateStateMachineJob()
		{
			return new StateMachinesExecutor(this);
		}

		private static T Retry<T>(int count, Func<T> act)
		{
			var exceptions = new List<Exception>();
			for(int i = 0; i < count; i++)
			{
				try
				{
					return act();
				}
				catch(Exception ex)
				{
					exceptions.Add(ex);
				}
			}
			throw new AggregateException(exceptions);
		}

		List<IDisposable> _Disposables = new List<IDisposable>();

		public void Dispose()
		{
			foreach(var disposable in _Disposables)
				disposable.Dispose();
			_Disposables.Clear();
		}


		public IDestinationWallet DestinationWallet
		{
			get; set;
		}

		public Network Network
		{
			get;
			set;
		}
		public ExternalServices Services
		{
			get;
			set;
		}
		public Tracker Tracker
		{
			get;
			set;
		}
		public ClassicTumblerParameters TumblerParameters
		{
			get;
			set;
		}
		public DBreezeRepository Repository
		{
			get;
			set;
		}
	}
}
