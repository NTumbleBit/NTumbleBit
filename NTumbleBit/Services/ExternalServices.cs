using NBitcoin.RPC;
using NTumbleBit.Services.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.Services
{
	public class ExternalServices
    {
		public static ExternalServices CreateFromRPCClient(RPCClient rpc, IRepository repository, Tracker tracker, bool aggregateFunding)
		{
			var info = rpc.SendCommand(RPCOperations.getinfo);
			var minimumRate = new NBitcoin.FeeRate(NBitcoin.Money.Coins((decimal)(double)((Newtonsoft.Json.Linq.JValue)(info.Result["relayfee"])).Value * 2), 1000);
			
			ExternalServices service = new ExternalServices();
			service.FeeService = new RPCFeeService(rpc) {
				MinimumFeeRate = minimumRate
			};

			// on regtest the estimatefee always fails
			if (rpc.Network == NBitcoin.Network.RegTest)
			{
				service.FeeService = new RPCFeeService(rpc)
				{
					MinimumFeeRate = minimumRate,
					FallBackFeeRate = new NBitcoin.FeeRate(NBitcoin.Money.Satoshis(50), 1)
				};
			}

			var cache = new RPCWalletCache(rpc, repository);
			service.WalletService = new RPCWalletService(rpc)
			{
				BatchInterval = aggregateFunding ? TimeSpan.FromSeconds(30) : TimeSpan.Zero
			};
			service.BroadcastService = new RPCBroadcastService(rpc, cache, repository);
			service.BlockExplorerService = new RPCBlockExplorerService(rpc, cache, repository)
			{
				BatchInterval = aggregateFunding ? TimeSpan.FromSeconds(5) : TimeSpan.Zero
			};
			service.TrustedBroadcastService = new RPCTrustedBroadcastService(rpc, service.BroadcastService, service.BlockExplorerService, repository, cache, tracker)
			{
				//BlockExplorer will already track the addresses, since they used a shared bitcoind, no need of tracking again (this would overwrite labels)
				TrackPreviousScriptPubKey = false
			};
			return service;
		}
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
		public ITrustedBroadcastService TrustedBroadcastService
		{
			get; set;
		}
	}
}
