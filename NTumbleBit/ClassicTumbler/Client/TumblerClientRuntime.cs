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
using TCPServer.Client;

namespace NTumbleBit.ClassicTumbler.Client
{
	public enum Identity
	{
		Alice,
		Bob
	}
	public class PrematureRequestException : Exception
	{
		public PrematureRequestException() : base("Premature request")
		{

		}
	}

	public class TumblerClientRuntime : IDisposable
	{
		public static TumblerClientRuntime FromConfiguration(TumblerClientConfigurationBase configuration, ClientInteraction interaction)
		{
			return FromConfigurationAsync(configuration, interaction).GetAwaiter().GetResult();
		}

		public static async Task<TumblerClientRuntime> FromConfigurationAsync(TumblerClientConfigurationBase configuration, ClientInteraction interaction)
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
		public async Task ConfigureAsync(TumblerClientConfigurationBase configuration, ClientInteraction interaction)
		{
			interaction = interaction ?? new AcceptAllClientInteraction();

			Network = configuration.Network;
			TumblerServer = configuration.TumblerServer;
			BobSettings = configuration.BobConnectionSettings;
			AliceSettings = configuration.AliceConnectionSettings;
			AllowInsecure = configuration.AllowInsecure;

			if (this.TumblerServer.IsOnion)
				await SetupTorAsync(interaction, configuration.TorPath).ConfigureAwait(false);
			else if (configuration.TorMandatory)
				throw new ConfigException("The tumbler server should use TOR");

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
			Services = ExternalServices.CreateFromRPCClient(rpc, dbreeze, Tracker, false);

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

			TumblerParameters = dbreeze.Get<ClassicTumbler.ClassicTumblerParameters>("Configuration", TumblerServer.ToString());

			if (TumblerParameters != null && TumblerParameters.GetHash() != configuration.TumblerServer.ConfigurationHash)
				TumblerParameters = null;

			if(!configuration.OnlyMonitor)
			{
				var client = CreateTumblerClient(0);
				if(TumblerParameters == null)
				{
					Logs.Configuration.LogInformation("Downloading tumbler information of " + configuration.TumblerServer.ToString());
					var parameters = Retry(3, () => client.GetTumblerParameters());
					if(parameters == null)
						throw new ConfigException("Unable to download tumbler's parameters");

					if(parameters.GetHash() != configuration.TumblerServer.ConfigurationHash)
						throw new ConfigException("The tumbler returned an invalid configuration");

					var standardCycles = new StandardCycles(configuration.Network);
					var standardCycle = standardCycles.GetStandardCycle(parameters);
					if(parameters.ExpectedAddress != TumblerServer.GetRoutableUri(false).AbsoluteUri)
						throw new ConfigException("This tumbler has parameters used for an unexpected uri");
					Logs.Configuration.LogInformation("Checking RSA key proof and standardness of the settings...");
					if(standardCycle == null || !parameters.IsStandard())
					{
						Logs.Configuration.LogWarning("This tumbler has non standard parameters");
						if(!AllowInsecure)
							throw new ConfigException("This tumbler has non standard parameters");
						standardCycle = null;
					}

					await interaction.ConfirmParametersAsync(parameters, standardCycle).ConfigureAwait(false);

					Repository.UpdateOrInsert("Configuration", TumblerServer.ToString(), parameters, (o, n) => n);
					TumblerParameters = parameters;

					Logs.Configuration.LogInformation("Tumbler parameters saved");
				}

				Logs.Configuration.LogInformation($"Using tumbler {TumblerServer.ToString()}");
			}
		}

		private async Task SetupTorAsync(ClientInteraction interaction, string torPath)
		{
			await SetupTorAsync(interaction, AliceSettings, torPath).ConfigureAwait(false);
			await SetupTorAsync(interaction, BobSettings, torPath).ConfigureAwait(false);
		}

		private async Task SetupTorAsync(ClientInteraction interaction, ConnectionSettingsBase settings, string torPath)
		{
			var tor = settings as ITorConnectionSettings;
			if(tor == null)
				throw new ConfigException("TOR Settings not properly configured");
			_Disposables.Add(await tor.SetupAsync(interaction, torPath).ConfigureAwait(false));
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

		public TumblerUrlBuilder TumblerServer
		{
			get; set;
		}

		public TumblerClient CreateTumblerClient(int cycle, Identity? identity = null)
		{
			if(identity == null)
				identity = RandomUtils.GetUInt32() % 2 == 0 ? Identity.Alice : Identity.Bob;
			return CreateTumblerClient(cycle, identity == Identity.Alice ? AliceSettings : BobSettings);
		}

		DateTimeOffset previousHandlerCreationDate;
		TimeSpan CircuitRenewInterval = TimeSpan.FromMinutes(10.0);
		private TumblerClient CreateTumblerClient(int cycleId, ConnectionSettingsBase settings)
		{
			if(!AllowInsecure && DateTimeOffset.UtcNow - previousHandlerCreationDate < CircuitRenewInterval)
			{
				throw new PrematureRequestException();
			}
			previousHandlerCreationDate = DateTime.UtcNow;
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
		public IExternalServices Services
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
		public bool AllowInsecure
		{
			get;
			private set;
		}
	}
}
