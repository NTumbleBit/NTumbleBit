using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#if !CLIENT
namespace NTumbleBit.TumblerServer.Models
#else
namespace NTumbleBit.Client.Tumbler.Models
#endif
{
	public class SignVoucherRequest
    {
		public MerkleBlock MerkleProof
		{
			get; set;
		}
		public Transaction Transaction
		{
			get; set;
		}
	}
}
