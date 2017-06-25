using NBitcoin;
using NTumbleBit.ClassicTumbler;
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
		public int Cycle
		{
			get; set;
		}
		public int KeyReference
		{
			get; set;
		}
		public PuzzleValue UnsignedVoucher
		{
			get; set;
		}
		public MerkleBlock MerkleProof
		{
			get; set;
		}
		public PubKey ClientEscrowKey
		{
			get; set;
		}
		public Transaction Transaction
		{
			get; set;
		}
	}
}
