using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using NTumbleBit.Client.Tumbler.Services;
using System.IO;
using NTumbleBit.Common;
using NBitcoin.RPC;
using NTumbleBit.Common.Logging;
using Microsoft.Extensions.Logging;
using NTumbleBit.ClassicTumbler;

namespace NTumbleBit.Client.Tumbler
{
	public class TumblerClientRuntime : IDisposable
	{
		public static TumblerClientRuntime FromConfiguration(TumblerClientConfiguration configuration, out bool needUserConfirmation)
		{
			needUserConfirmation = false;
			var runtime = new TumblerClientRuntime();
			try
			{
				runtime.Network = configuration.Network;
				RPCClient rpc = null;
				try
				{
					rpc = configuration.RPCArgs.ConfigureRPCClient(configuration.Network);
				}
				catch
				{
					throw new ConfigException("Please, fix rpc settings in " + configuration.ConfigurationFile);
				}

				var dbreeze = new DBreezeRepository(Path.Combine(configuration.DataDir, "db"));
				runtime.Cooperative = configuration.Cooperative;
				runtime.Repository = dbreeze;
				runtime._Disposables.Add(dbreeze);
				runtime.Tracker = new Tracker(dbreeze);
				runtime.Services = ExternalServices.CreateFromRPCClient(rpc, dbreeze, runtime.Tracker);


				if(configuration.OutputWallet.RootKey != null && configuration.OutputWallet.KeyPath != null)
					runtime.DestinationWallet = new ClientDestinationWallet("", configuration.OutputWallet.RootKey, configuration.OutputWallet.KeyPath, dbreeze);
				else if(configuration.OutputWallet.RPCArgs != null)
				{
					try
					{
						runtime.DestinationWallet = new RPCDestinationWallet(configuration.OutputWallet.RPCArgs.ConfigureRPCClient(runtime.Network));
					}
					catch
					{
						throw new ConfigException("Please, fix outputwallet rpc settings in " + configuration.ConfigurationFile);
					}
				}
				else
					throw new ConfigException("Missing configuration for outputwallet");

				var existingConfig = dbreeze.Get<ClassicTumbler.ClassicTumblerParameters>("Configuration", configuration.TumblerServer.AbsoluteUri);
				if(!configuration.OnlyMonitor)
				{
					runtime.TumblerClient = new TumblerClient(runtime.Network, configuration.TumblerServer);

					Logs.Configuration.LogInformation("Downloading tumbler information of " + configuration.TumblerServer.AbsoluteUri);
					var parameters = Retry(3, () => runtime.TumblerClient.GetTumblerParameters());
					Logs.Configuration.LogInformation("Tumbler Server Connection successfull");

					if(existingConfig != null)
					{
						if(Serializer.ToString(existingConfig) != Serializer.ToString(parameters))
						{
							needUserConfirmation = true;
						}
					}
					else
					{
						dbreeze.UpdateOrInsert("Configuration", configuration.TumblerServer.AbsoluteUri, parameters, (o, n) => n);
					}
					runtime.TumblerParameters = parameters;
				}
				else
				{
					runtime.TumblerParameters = existingConfig;
				}
			}
			catch
			{
				runtime.Dispose();
				throw;
			}
			return runtime;
		}

		public BroadcasterJob CreateBroadcasterJob()
		{
			return new BroadcasterJob(Services, Logs.Main);
		}

		public bool Cooperative
		{
			get; set;
		}

		public StateMachinesExecutor CreateStateMachineJob()
		{
			return new StateMachinesExecutor(TumblerParameters, TumblerClient, DestinationWallet, Services, Repository, Logs.Main, Tracker)
			{
				Cooperative = Cooperative
			};
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
		public TumblerClient TumblerClient
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
