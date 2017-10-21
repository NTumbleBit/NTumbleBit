using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class PuzzlePaymentRequest
	{
		public PuzzlePaymentRequest(Puzzle puzzle, Money amount, LockTime escrowDate)
		{
			if(puzzle == null)
				throw new ArgumentNullException(nameof(puzzle));
            Amount = amount ?? throw new ArgumentNullException(nameof(amount));
			EscrowDate = escrowDate;
			RsaPubKeyHash = puzzle.RsaPubKey.GetHash();
			PuzzleValue = puzzle.PuzzleValue;
		}
		public uint256 RsaPubKeyHash
		{
			get; set;
		}
		public PuzzleValue PuzzleValue
		{
			get; set;
		}
		public Money Amount
		{
			get; set;
		}
		public LockTime EscrowDate
		{
			get; set;
		}
	}
}

