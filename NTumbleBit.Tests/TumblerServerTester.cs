using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.Extensions.Configuration;
using NBitcoin;
using NTumbleBit.Client.Tumbler;
using NTumbleBit.TumblerServer;
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
					if(process.MainModule.FileName.Replace("\\", "/").StartsWith(Path.GetFullPath(rootTestData).Replace("\\", "/")))
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

			_TumblerNode.Sync(_AliceNode, true);
			_TumblerNode.Sync(_BobNode, true);
			_BobNode.Sync(_AliceNode, true);

			var rpc = _TumblerNode.CreateRPCClient();

			var conf = new TumblerConfiguration();
			conf.Network = Network.RegTest;
			conf.RPCClient = rpc;
			conf.CycleParameters.Start = 105;

			_Host = new WebHostBuilder()
				.UseKestrel()
				.UseAppConfiguration(conf)
				.UseContentRoot(Path.GetFullPath(directory))
				.UseIISIntegration()
				.UseStartup<Startup>()
				.Build();

			_Host.Start();
		}

		private static bool TryDelete(string directory, bool throws)
		{
			try
			{
				Directory.Delete(directory, true);
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

		public TumblerConfiguration TumblerConfiguration
		{
			get
			{
				return (TumblerConfiguration)_Host.Services.GetService(typeof(TumblerConfiguration));
			}
		}

		public TumblerClient CreateTumblerClient()
		{
			return new TumblerClient(TumblerConfiguration.Network, Address);
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
