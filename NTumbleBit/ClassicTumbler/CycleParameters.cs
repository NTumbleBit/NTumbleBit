using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
	public enum CyclePhase : int
	{
		Registration,
		ClientChannelEstablishment,
		TumblerChannelEstablishment,
		PaymentPhase,
		TumblerCashoutPhase,
		ClientCashoutPhase
	}

	public class CyclePeriod
	{
		public CyclePeriod(int start, int end)
		{
			if(start > end)
				throw new ArgumentException("start should be inferiod to end");
			Start = start;
			End = end;
		}
		public CyclePeriod()
		{

		}
		public int Start
		{
			get; set;
		}

		public int End
		{
			get; set;
		}

		public bool IsInPeriod(int blockHeight)
		{
			return Start <= blockHeight && blockHeight < End;
		}
	}
	public class CyclePeriods
	{
		public CyclePeriod Registration
		{
			get; set;
		}
		public CyclePeriod ClientChannelEstablishment
		{
			get; set;
		}
		public CyclePeriod TumblerChannelEstablishment
		{
			get; set;
		}
		public CyclePeriod TumblerCashout
		{
			get; set;
		}
		public CyclePeriod ClientCashout
		{
			get; set;
		}

		public CyclePeriod Total
		{
			get; set;
		}
		public CyclePeriod Payment
		{
			get;
			internal set;
		}

		public bool IsInPhase(CyclePhase phase, int blockHeight)
		{
			return GetPeriod(phase).IsInPeriod(blockHeight);
		}

		public CyclePeriod GetPeriod(CyclePhase phase)
		{
			switch(phase)
			{
				case CyclePhase.Registration:
					return Registration;
				case CyclePhase.ClientChannelEstablishment:
					return ClientChannelEstablishment;
				case CyclePhase.TumblerChannelEstablishment:
					return TumblerChannelEstablishment;
				case CyclePhase.TumblerCashoutPhase:
					return TumblerCashout;
				case CyclePhase.PaymentPhase:
					return Payment;
				case CyclePhase.ClientCashoutPhase:
					return ClientCashout;
				default:
					throw new NotSupportedException();
			}
		}

		public bool IsInside(int blockHeight)
		{
			return Total.IsInPeriod(blockHeight);
		}
	}


	/// <summary>
	/// See https://medium.com/@nicolasdorier/tumblebit-tumbler-mode-ea44e9a2a2ec#.d1kq6t2px
	/// </summary>
	public class CycleParameters
	{
		public CycleParameters()
		{
			Start = 0;
			RegistrationDuration = 18;
			ClientChannelEstablishmentDuration = 3;
			TumblerChannelEstablishmentDuration = 3;
			SafetyPeriodDuration = 2;
			PaymentPhaseDuration = 3;
			TumblerCashoutDuration = 18;
			ClientCashoutDuration = 18;
		}

		public CyclePeriods GetPeriods()
		{
			int registrationStart = Start;
			int registrationEnd = registrationStart + RegistrationDuration;
			int cchannelRegistrationStart = registrationEnd;
			int cchannelRegistrationEnd = cchannelRegistrationStart + ClientChannelEstablishmentDuration;
			int tchannelRegistrationStart = cchannelRegistrationEnd;
			int tchannelRegistrationEnd = tchannelRegistrationStart + TumblerChannelEstablishmentDuration;
			int tcashoutStart = tchannelRegistrationEnd + SafetyPeriodDuration;
			int tcashoutEnd = tcashoutStart + TumblerCashoutDuration;
			int paymentStart = tcashoutStart;
			int paymentEnd = paymentStart + PaymentPhaseDuration;
			int ccashoutStart = tcashoutEnd;
			int ccashoutEnd = ccashoutStart + ClientCashoutDuration;
			CyclePeriods periods = new CyclePeriods();
			periods.Registration = new CyclePeriod(registrationStart, registrationEnd);
			periods.ClientChannelEstablishment = new CyclePeriod(cchannelRegistrationStart, cchannelRegistrationEnd);
			periods.TumblerChannelEstablishment = new CyclePeriod(tchannelRegistrationStart, tchannelRegistrationEnd);
			periods.TumblerCashout = new CyclePeriod(tcashoutStart, tcashoutEnd);
			periods.Payment = new CyclePeriod(paymentStart, paymentEnd);
			periods.ClientCashout = new CyclePeriod(ccashoutStart, ccashoutEnd);
			periods.Total = new CyclePeriod(Start, ccashoutEnd + SafetyPeriodDuration);
			return periods;
		}

		public bool IsInPhase(CyclePhase phase, int blockHeight)
		{
			var periods = GetPeriods();
			return periods.IsInPhase(phase, blockHeight);
		}

		public bool IsInside(int blockHeight)
		{
			var periods = GetPeriods();
			return periods.IsInside(blockHeight);
		}

		public LockTime GetClientLockTime()
		{
			var periods = GetPeriods();
			var lockTime = new LockTime(periods.ClientCashout.Start + SafetyPeriodDuration - 1);
			if(lockTime.IsTimeLock)
				throw new InvalidOperationException("Invalid cycle");
			return lockTime;
		}

		public LockTime GetTumblerLockTime()
		{
			var periods = GetPeriods();
			var lockTime = new LockTime(periods.ClientCashout.End + SafetyPeriodDuration - 1);
			if(lockTime.IsTimeLock)
				throw new InvalidOperationException("Invalid cycle");
			return lockTime;
		}

		public CycleParameters Clone()
		{
			return new CycleParameters()
			{
				ClientCashoutDuration = ClientCashoutDuration,
				SafetyPeriodDuration = SafetyPeriodDuration,
				Start = Start,
				ClientChannelEstablishmentDuration = ClientChannelEstablishmentDuration,
				PaymentPhaseDuration = PaymentPhaseDuration,
				RegistrationDuration = RegistrationDuration,
				TumblerCashoutDuration = TumblerCashoutDuration,
				TumblerChannelEstablishmentDuration = TumblerChannelEstablishmentDuration
			};
		}

		public int Start
		{
			get; set;
		}

		public int RegistrationDuration
		{
			get; set;
		}
		public int ClientChannelEstablishmentDuration
		{
			get; set;
		}

		public int TumblerChannelEstablishmentDuration
		{
			get; set;
		}

		public int TumblerCashoutDuration
		{
			get; set;
		}

		public int ClientCashoutDuration
		{
			get; set;
		}

		public int PaymentPhaseDuration
		{
			get; set;
		}

		public int SafetyPeriodDuration
		{
			get; set;
		}
	}
}
