using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.Extensions.Configuration;
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
			if(Directory.Exists(rootTestData))
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

			var rpc = _TumblerNode.CreateRPCClient();
			var confBuilder = new ConfigurationBuilder();
			confBuilder.AddInMemoryCollection(new[] {
				new KeyValuePair<string,string>( "NTumbleBit:Network", "regtest" ),
				new KeyValuePair<string,string>( "NTumbleBit:RPC:Address", rpc.Address.AbsoluteUri),
				new KeyValuePair<string,string>( "NTumbleBit:RPC:Username", rpc.Credentials.UserName),
				new KeyValuePair<string,string>( "NTumbleBit:RPC:Password", rpc.Credentials.Password)
			});
			_Host = new WebHostBuilder()
				.UseKestrel()
				.UseConfiguration(confBuilder.Build())
				.UseAppConfiguration(confBuilder)
				.UseContentRoot(Path.GetFullPath(directory))
				.UseIISIntegration()
				.UseStartup<Startup>()
				.Build();

			new Thread(() => _Host.Run(_StopHost.Token)).Start();
			_NodeBuilder.StartAll();
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

		CancellationTokenSource _StopHost = new CancellationTokenSource();

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
			_StopHost.Cancel();
			_NodeBuilder.Dispose();
		}
	}
}
