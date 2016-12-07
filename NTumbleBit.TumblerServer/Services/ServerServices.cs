using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.TumblerServer.Services
{
    public class ServerServices
    {
		public IFeeService FeeService
		{
			get; set;
		}
		public IWalletService WalletService
		{
			get; set;
		}
		public IBroadcastService BroadcastService
		{
			get; set;
		}
		public IBlockExplorerService BlockExplorerService
		{
			get; set;
		}
	}
}
