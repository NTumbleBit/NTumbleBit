﻿using System;
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

namespace NTumbleBit.ClassicTumbler.Client
{
	public class TumblerClientRuntime : IDisposable
	{
		public static TumblerClientRuntime FromConfiguration(TumblerClientConfiguration configuration, out ClassicTumblerParameters parametersToConfirm)
		{
			parametersToConfirm = null;
			bool needUserConfirmation = false;
			var runtime = new TumblerClientRuntime();
			try
			{
				runtime.Network = configuration.Network;
				runtime.TumblerServer = configuration.TumblerServer;
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
				runtime.Cooperative = configuration.Cooperative;
				runtime.Repository = dbreeze;
				runtime._Disposables.Add(dbreeze);
				runtime.Tracker = new Tracker(dbreeze, runtime.Network);
				runtime.Services = ExternalServices.CreateFromRPCClient(rpc, dbreeze, runtime.Tracker);

				if(configuration.OutputWallet.RootKey != null && configuration.OutputWallet.KeyPath != null)
					runtime.DestinationWallet = new ClientDestinationWallet(configuration.OutputWallet.RootKey, configuration.OutputWallet.KeyPath, dbreeze, configuration.Network);
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
					var client = runtime.CreateTumblerClient();

					Logs.Configuration.LogInformation("Downloading tumbler information of " + configuration.TumblerServer.AbsoluteUri);
					var parameters = Retry(3, () => client.GetTumblerParameters(new Identity(Role.Alice, -1)));
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
						needUserConfirmation = true;
					}
					if(needUserConfirmation)
						parametersToConfirm = parameters;
					else
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

		private static async Task<IPAddress> GetExternalIp(TumblerClient client, string url)
		{
			var result = await client.Client.GetAsync(url).ConfigureAwait(false);
			var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
			return IPAddress.Parse(content.Replace("\n", string.Empty));
		}

		public void Confirm(ClassicTumblerParameters parameters)
		{
			Repository.UpdateOrInsert("Configuration", TumblerServer.AbsoluteUri, parameters, (o, n) => n);
			TumblerParameters = parameters;
		}

		public BroadcasterJob CreateBroadcasterJob()
		{
			return new BroadcasterJob(Services);
		}

		public bool Cooperative
		{
			get; set;
		}

		public Uri TumblerServer
		{
			get; set;
		}

		public TumblerClient CreateTumblerClient()
		{
			var client = new TumblerClient(Network, TumblerServer);
			if (Tor.UseTor)
			{
				client.SetHttpHandler(Tor.SocksPortHandler);
			}
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
