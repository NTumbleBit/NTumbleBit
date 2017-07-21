
namespace NTumbleBit.Services
{
	/// <summary>
	/// Interface containing objects used for interacting with a bitcoin node.
	/// </summary>
	public interface IExternalServices
	{        
		IFeeService FeeService { get; set; }
	
		IWalletService WalletService { get; set; }

		IBroadcastService BroadcastService { get; set; }
		
		IBlockExplorerService BlockExplorerService { get; set; }
		
		ITrustedBroadcastService TrustedBroadcastService { get; set; }
	}
}
