using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.TumblerServer.Options
{
    public class NTumbleBitOptions
    {
		public string Network
		{
			get; set;
		}
		public RPCSettings RPC
		{
			get; set;
		}
	}

	public class RPCSettings
	{
		public string Address
		{
			get; set;
		}
		public string Username
		{
			get; set;
		}
		public string Password
		{
			get; set;
		}
	}
}
