using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.Extensions.Configuration;
using NTumbleBit.Client.Tumbler;
using NTumbleBit.TumblerServer;
using System;
using System.Collections.Generic;
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
			directory = "TestData/" + directory;
			_Directory = directory;
			if(Directory.Exists("TestData"))
				Directory.CreateDirectory("TestData");
			try
			{
				Directory.Delete(directory, true);
			}
			catch(DirectoryNotFoundException)
			{
			}
			_NodeBuilder = NodeBuilder.Create(directory);
			_CoreNode = _NodeBuilder.CreateNode(true);
			Directory.CreateDirectory(directory);

			var rpc = _CoreNode.CreateRPCClient();
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
				.UseHostingConfiguration(confBuilder)
				.UseContentRoot(Path.GetFullPath(directory))
				.UseIISIntegration()
				.UseStartup<Startup>()
				.Build();

			new Thread(() => _Host.Run(_StopHost.Token)).Start();
		}


		private readonly CoreNode _CoreNode;
		public CoreNode CoreNode
		{
			get
			{
				return _CoreNode;
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
