using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.Client
{
    public class TorParameters
	{
		public string Host { get; set; }
		public int SocksPort { get; set; }
		public int ControlPort { get; set; }
		public string ControlPortPassword { get; set; }
	}
}
