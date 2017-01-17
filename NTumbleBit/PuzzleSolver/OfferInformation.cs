using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzleSolver
{
    public class OfferInformation
    {
		public Money Fee
		{
			get;
			set;
		}
		public LockTime LockTime
		{
			get; set;
		}
		public PubKey FulfillKey
		{
			get; set;
		}
		public TransactionSignature Signature
		{
			get; set;
		}
	}
}
