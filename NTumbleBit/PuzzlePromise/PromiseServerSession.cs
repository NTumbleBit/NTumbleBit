using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
	public class PromiseServerSession
	{
		public PromiseServerSession(PromiseParameters parameters = null)
		{
			_Parameters = parameters ?? new PromiseParameters();
		}

		private readonly PromiseParameters _Parameters;
		public PromiseParameters Parameters
		{
			get
			{
				return _Parameters;
			}
		}

		public void SignAndEncrypt(uint256[] hashes)
		{
			if(hashes == null)
				throw new ArgumentNullException("hashes");
			if(hashes.Length != Parameters.GetTotalTransactionsCount())
				throw new ArgumentException("Incorrect number of hashes, expected " + hashes.Length);



			//TransactionBuilder builder = new TransactionBuilder();
			//builder.AddCoins(unspentCoins);

			//var refundCondition = new Script(OpcodeType.);

		}

	}
}
