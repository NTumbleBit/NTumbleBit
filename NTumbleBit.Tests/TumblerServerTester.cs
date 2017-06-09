using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.Extensions.Configuration;
using NBitcoin;
using NBitcoin.RPC;
using NTumbleBit.Client.Tumbler;
using NTumbleBit.TumblerServer;
using NTumbleBit.TumblerServer.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NTumbleBit.Tests
{
	public class TumblerClientContext
	{
		public TumblerClientContext(TumblerClient tumblerClient, RPCClient rpcClient, Client.Tumbler.Services.IRepository clientRepository)
		{
			var parameters = tumblerClient.GetTumblerParameters();
			PaymentMachineState = new PaymentStateMachine(
				parameters,
				tumblerClient,
				new ClientDestinationWallet("", new ExtKey().Neuter().GetWif(rpcClient.Network), new KeyPath(), clientRepository),
				Client.Tumbler.Services.ExternalServices.CreateFromRPCClient(rpcClient, clientRepository)
				);
		}
		public PaymentStateMachine PaymentMachineState
		{
			get;
			private set;
		}

		public Client.Tumbler.Services.RPCServices.RPCBlockExplorerService BlockExplorer
		{
			get
			{
				return (Client.Tumbler.Services.RPCServices.RPCBlockExplorerService)PaymentMachineState.Services.BlockExplorerService;
			}
		}

		public Client.Tumbler.Services.RPCServices.RPCBroadcastService UntrustedBroadcaster
		{
			get
			{
				return (Client.Tumbler.Services.RPCServices.RPCBroadcastService)PaymentMachineState.Services.BroadcastService;
			}
		}

		public Client.Tumbler.Services.RPCServices.RPCTrustedBroadcastService TrustedBroadcastService
		{
			get
			{
				return (Client.Tumbler.Services.RPCServices.RPCTrustedBroadcastService)PaymentMachineState.Services.TrustedBroadcastService;
			}
		}
	}
	public class TumblerServerContext
	{
		private IWebHost _Host;

		public TumblerServerContext(IWebHost _Host)
		{
			this._Host = _Host;
		}

		public ClassicTumblerRepository TumblerRepository
		{
			get
			{
				return (ClassicTumblerRepository)_Host.Services.GetService(typeof(ClassicTumblerRepository));
			}
		}

		public TumblerConfiguration TumblerConfiguration
		{
			get
			{
				return (TumblerConfiguration)_Host.Services.GetService(typeof(TumblerConfiguration));
			}
		}

		public T GetService<T>()
		{
			return (T)_Host.Services.GetService(typeof(T));
		}

		public ExternalServices ExtenalServices
		{
			get
			{
				return (ExternalServices)_Host.Services.GetService(typeof(ExternalServices));
			}
		}

		public TumblerServer.Services.RPCServices.RPCBroadcastService BroadcastService
		{
			get
			{
				return (TumblerServer.Services.RPCServices.RPCBroadcastService)ExtenalServices.BroadcastService;
			}
		}

		public TumblerServer.Services.RPCServices.RPCBlockExplorerService BlockExplorer
		{
			get
			{
				return (TumblerServer.Services.RPCServices.RPCBlockExplorerService)ExtenalServices.BlockExplorerService;
			}
		}

		public TumblerServer.Services.RPCServices.RPCTrustedBroadcastService TrustedBroadcastService
		{
			get
			{
				return (TumblerServer.Services.RPCServices.RPCTrustedBroadcastService)ExtenalServices.TrustedBroadcastService;
			}
		}
	}
	public class TumblerServerTester : IDisposable
	{
		public static TumblerServerTester Create([CallerMemberNameAttribute]string caller = null)
		{
			return new TumblerServerTester(caller);
		}
		public TumblerServerTester(string directory)
		{
			var rootTestData = "TestData";
			directory = rootTestData + "/" + directory;
			_Directory = directory;
			if(!Directory.Exists(rootTestData))
				Directory.CreateDirectory(rootTestData);

			if(!TryDelete(directory, false))
			{
				foreach(var process in Process.GetProcessesByName("bitcoind"))
				{
					if(process.MainModule.FileName.Replace("\\", "/").StartsWith(Path.GetFullPath(rootTestData).Replace("\\", "/"), StringComparison.Ordinal))
					{
						process.Kill();
						process.WaitForExit();
					}
				}
				TryDelete(directory, true);
			}

			_NodeBuilder = NodeBuilder.Create(directory);
			_TumblerNode = _NodeBuilder.CreateNode(false);
			_AliceNode = _NodeBuilder.CreateNode(false);
			_BobNode = _NodeBuilder.CreateNode(false);

			Directory.CreateDirectory(directory);

			_NodeBuilder.StartAll();

			SyncNodes();

			var rpc = _TumblerNode.CreateRPCClient();

			var conf = new TumblerConfiguration();
			conf.DataDir = directory;
			conf.Network = Network.RegTest;
			conf.RPCClient = rpc;
			conf.TumblerKey = TestKeys.Default;
			conf.VoucherKey = TestKeys.Default2;
			conf.ClassicTumblerParameters.FakePuzzleCount /= 4;
			conf.ClassicTumblerParameters.FakeTransactionCount /= 4;
			conf.ClassicTumblerParameters.RealTransactionCount /= 4;
			conf.ClassicTumblerParameters.RealPuzzleCount /= 4;
			conf.ClassicTumblerParameters.CycleGenerator.FirstCycle.Start = 105;

			_Host = new WebHostBuilder()
				.UseKestrel()
				.UseAppConfiguration(conf)
				.UseContentRoot(Path.GetFullPath(directory))
				.UseIISIntegration()
				.UseStartup<Startup>()
				.Build();

			_Host.Start();

			ServerContext = new TumblerServerContext(_Host);

			//Overrides server fee
			((TumblerServer.Services.RPCServices.RPCFeeService)ServerContext.ExtenalServices.FeeService).FallBackFeeRate = new FeeRate(Money.Satoshis(100), 1);

			var repo = new Client.Tumbler.Services.DBreezeRepository(Path.Combine(directory, "client"));
			ClientContext = new TumblerClientContext(CreateTumblerClient(), AliceNode.CreateRPCClient(), repo);

			//Overrides client fee
			((Client.Tumbler.Services.RPCServices.RPCFeeService)ClientContext.PaymentMachineState.Services.FeeService).FallBackFeeRate = new FeeRate(Money.Satoshis(50), 1);
		}

		public TumblerClientContext ClientContext
		{
			get; set;
		}

		public TumblerServerContext ServerContext
		{
			get; set;
		}

		public void SyncNodes()
		{
			foreach(var node in NodeBuilder.Nodes)
			{
				foreach(var node2 in NodeBuilder.Nodes)
				{
					if(node != node2)
						node.Sync(node2, true);
				}
			}
		}

		private static bool TryDelete(string directory, bool throws)
		{
			try
			{
				Utils.DeleteRecursivelyWithMagicDust(directory);
				return true;
			}
			catch(DirectoryNotFoundException)
			{
				return true;
			}
			catch(Exception)
			{
				if(throws)
					throw;
			}
			return false;
		}

		private readonly CoreNode _TumblerNode;
		public CoreNode TumblerNode
		{
			get
			{
				return _TumblerNode;
			}
		}

		private readonly NodeBuilder _NodeBuilder;
		public NodeBuilder NodeBuilder
		{
			get
			{
				return _NodeBuilder;
			}
		}

		private readonly IWebHost _Host;
		public IWebHost Host
		{
			get
			{
				return _Host;
			}
		}

		public Uri Address
		{
			get
			{

				var address = ((KestrelServer)(_Host.Services.GetService(typeof(IServer)))).Features.Get<IServerAddressesFeature>().Addresses.FirstOrDefault();
				return new Uri(address);
			}
		}

		public TumblerClient CreateTumblerClient()
		{
			return new TumblerClient(ServerContext.TumblerConfiguration.Network, Address);
		}

		private readonly string _Directory;
		private readonly CoreNode _AliceNode;
		public CoreNode AliceNode
		{
			get
			{
				return _AliceNode;
			}
		}


		private readonly CoreNode _BobNode;
		public CoreNode BobNode
		{
			get
			{
				return _BobNode;
			}
		}

		public string BaseDirectory
		{
			get
			{
				return _Directory;
			}
		}

		public void Dispose()
		{
			_Host.Dispose();
			_NodeBuilder.Dispose();
		}
	}
}
