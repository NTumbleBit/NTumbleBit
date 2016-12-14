using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin.RPC;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.TumblerServer.Services;

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

		public ClassicTumblerParameters CreateClassicTumblerParameters()
		{
			var clone = Serializer.Clone(ClassicTumblerParameters);
			clone.ServerKey = TumblerKey.PubKey;
			clone.VoucherKey = VoucherKey.PubKey;
			return clone;
		}
	}
}
