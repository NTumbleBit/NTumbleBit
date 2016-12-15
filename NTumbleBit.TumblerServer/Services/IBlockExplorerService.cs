using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

#if !CLIENT
namespace NTumbleBit.TumblerServer.Services
#else
namespace NTumbleBit.Client.Tumbler.Services
#endif
{
	public class TransactionInformation
	{
		public int Confirmations
		{
			get; set;
		}
		public MerkleBlock MerkleProof
		{
			get;
			set;
		}
		public Transaction Transaction
		{
			get; set;
		}
	}
    public interface IBlockExplorerService
    {
		int GetCurrentHeight();
		TransactionInformation[] GetTransactions(Script scriptPubKey, bool withProof);
		void Track(string label, Script scriptPubkey);
		int GetBlockConfirmations(uint256 blockId);
		bool TrackPrunedTransaction(Transaction transaction, MerkleBlock merkleProof);
	}
}
