using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
    public class CycleParameters
    {
		public int Start
		{
			get; set;
		}		

		public int ConfirmationDuration
		{
			get; set;
		}

		public int PaymentDuration
		{
			get; set;
		}

		public int TumblerCashoutDuration
		{
			get; set;
		}

		public int CashoutDuration
		{
			get; set;
		}

		public int AliceEscrowDuration
		{
			get; set;
		}

		public int TumblerEscrowDuration
		{
			get; set;
		}

		public int GetAliceLockTimeOffset()
		{
			return AliceEscrowDuration + TumblerEscrowDuration + 
				ConfirmationDuration + 
				PaymentDuration + 
				TumblerCashoutDuration;
		}
		public int GetTumblerLockTimeOffset()
		{
			return GetAliceLockTimeOffset() + CashoutDuration + ConfirmationDuration;
		}
	}
}
