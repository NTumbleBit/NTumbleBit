using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#if !CLIENT
namespace NTumbleBit.TumblerServer.Services
#else
namespace NTumbleBit.Client.Tumbler.Services
#endif
{
	public class ExternalServices
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
		public ILockTimedBroadcastService LockTimedBroadcastService
		{
			get; set;
		}
	}
}
