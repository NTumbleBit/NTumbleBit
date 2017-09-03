using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using System.Threading;

namespace NTumbleBit.Services
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
		Task<ICollection<TransactionInformation>> GetTransactionsAsync(Script scriptPubKey, bool withProof);
		TransactionInformation GetTransaction(uint256 txId);
		uint256 WaitBlock(uint256 currentBlock, CancellationToken cancellation);
		Task TrackAsync(Script scriptPubkey);
		int GetBlockConfirmations(uint256 blockId);
		Task<bool> TrackPrunedTransactionAsync(Transaction transaction, MerkleBlock merkleProof);
	}
}
