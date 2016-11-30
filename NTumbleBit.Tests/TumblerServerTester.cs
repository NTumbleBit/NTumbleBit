using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel;
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
			Directory.CreateDirectory(directory);


			_Host = new WebHostBuilder()
				.UseKestrel()
				.UseContentRoot(Path.GetFullPath(directory))
				.UseIISIntegration()
				.UseStartup<Startup>()
				.Build();

			new Thread(() => _Host.Run(_StopHost.Token)).Start();
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

		public Uri GetAddress()
		{
			var address = ((KestrelServer)(_Host.Services.GetService(typeof(IServer)))).Features.Get<IServerAddressesFeature>().Addresses.FirstOrDefault();
			return new Uri(address);
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
			return new TumblerClient(TumblerConfiguration.Network, GetAddress());
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
		}		
	}
}
