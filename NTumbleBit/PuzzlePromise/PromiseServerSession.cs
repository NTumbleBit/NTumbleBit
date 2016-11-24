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
			_Parameters = parameters ?? PromiseParameters.CreateDefault();
		}

		private readonly PromiseParameters _Parameters;
		public PromiseParameters Parameters
		{
			get
			{
				return _Parameters;
			}
		}

		public void CreateEscrowTransaction(ICoin[] unspentCoins, PubKey destination, Money amount, LockTime expiration)
		{
			if(unspentCoins == null)
				throw new ArgumentNullException("unspentCoins");
			if(destination == null)
				throw new ArgumentNullException("destination");
			if(amount == null)
				throw new ArgumentNullException("amount");
			if(expiration == null)
				throw new ArgumentNullException("expiration");

			//TransactionBuilder builder = new TransactionBuilder();
			//builder.AddCoins(unspentCoins);

			//var refundCondition = new Script(OpcodeType.);

		}

	}
}
