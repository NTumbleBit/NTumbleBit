using NBitcoin;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin.RPC;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.TumblerServer.Services;
using NTumbleBit.Common;
using System;

namespace NTumbleBit.TumblerServer
{

    public class TumblerConfiguration
    {
		public TumblerConfiguration()
		{			
			ClassicTumblerParameters = new ClassicTumblerParameters();
		}

		public string DataDirectory
		{
			get; set;
		}

		public RsaKey TumblerKey
		{
			get; set;
		}

		public RsaKey VoucherKey
		{
			get; set;
		}
		public Network Network
		{
			get
			{
				return ClassicTumblerParameters.Network;
			}
			set
			{
				ClassicTumblerParameters.Network = value;
			}
		}
		public RPCClient RPCClient
		{
			get;
			set;
		}

		public ClassicTumblerParameters ClassicTumblerParameters
		{
			get; set;
		}
		public string ConfigurationFile
		{
			get;
			set;
		}

		public string Port
		{
			get;
			set;
		} = "5000";

		public string Listen
		{
			get;
			set;
		}  = "0.0.0.0";

		public void ConfigureTestnet()
		{
			this.Network = Network.TestNet;
				var cycle = this
					.ClassicTumblerParameters
					.CycleGenerator.FirstCycle;

				cycle.RegistrationDuration = 3;
				cycle.Start = 0;
				cycle.RegistrationDuration = 3;
				cycle.ClientChannelEstablishmentDuration = 3;
				cycle.TumblerChannelEstablishmentDuration = 3;
				cycle.SafetyPeriodDuration = 2;
				cycle.PaymentPhaseDuration = 3;
				cycle.TumblerCashoutDuration = 4;
				cycle.ClientCashoutDuration = 3;
		}

		public void GetArgs(String[] args)
		{

			this.ConfigurationFile = args.Where(a => a.StartsWith("-conf=")).Select(a => a.Substring("-conf=".Length).Replace("\"", "")).FirstOrDefault();
			this.DataDirectory = args.Where(a => a.StartsWith("-datadir=")).Select(a => a.Substring("-datadir=".Length).Replace("\"", "")).FirstOrDefault();

			if(this.DataDirectory != null && this.ConfigurationFile != null )
			{
				var isRelativePath = Path.GetFullPath(this.ConfigurationFile).Length > this.ConfigurationFile.Length;
				if(isRelativePath)
				{
					this.ConfigurationFile = Path.Combine(this.DataDirectory, this.ConfigurationFile);
				}
			}
			
			if (this.ConfigurationFile != null && this.DataDirectory != null)
			{
				if(!File.Exists(this.ConfigurationFile))
					throw new ConfigurationException("Configuration file does not exists");
				var configTemp = TextFileConfiguration.Parse(File.ReadAllText(this.ConfigurationFile));				
				
				if (configTemp.ContainsKey("testnet"))
				{
					if (configTemp["testnet"] == "1")
					{
						this.ConfigureTestnet();
					}
				}
				// handle new text arguments here : 
				this.Port = (configTemp.ContainsKey("port") ? configTemp["port"] : "5000");
				this.Listen = (configTemp.ContainsKey("listen") ? configTemp["listen"] : "0.0.0.0");
			}
				
			
			if(args.Contains("-testnet", StringComparer.CurrentCultureIgnoreCase))
			{
				this.ConfigureTestnet();
			}
			
			// handle new arguments here : 
			string TempPort = args.Where(a => a.StartsWith("-port=")).Select(a => a.Substring("-port=".Length).Replace("\"", "")).FirstOrDefault();
			this.Port = (String.IsNullOrEmpty(TempPort) ? this.Port : TempPort);
			
			string TempListen = args.Where(a => a.StartsWith("-listen=")).Select(a => a.Substring("-listen=".Length).Replace("\"", "")).FirstOrDefault();
			this.Listen = (String.IsNullOrEmpty(TempListen) ? this.Listen : TempListen);

			
		}

		public ClassicTumblerParameters CreateClassicTumblerParameters()
		{
			var clone = Serializer.Clone(ClassicTumblerParameters);
			clone.ServerKey = TumblerKey.PubKey;
			clone.VoucherKey = VoucherKey.PubKey;
			return clone;
		}
	}
}

