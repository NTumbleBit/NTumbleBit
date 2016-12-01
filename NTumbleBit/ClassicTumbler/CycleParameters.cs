using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
	public enum CyclePhase : int
	{
		AliceEscrowPhase = 1,
		TumblerEscrowPhase = 2,
		TumblerEscrowConfirmation = 3,
		AlicePaymentPhase = 4,
		TumblerCashoutPhase = 5,
		BobCashoutPhase = 6,
		BobCashoutConfirmation = 7
	}

	public class CyclePhaseInformation
	{
		public CyclePhase Phase
		{
			get; set;
		}
		public int RemainingBlock
		{
			get; set;
		}
		public int Cycle
		{
			get; set;
		}
		public int Offset
		{
			get;
			set;
		}
	}
	public class CycleParameters
	{
		public CycleParameters()
		{
			// 1 day
			Start = 440000;
			BobCashoutDuration = 38;
			AliceEscrowDuration = 19;
			TumblerEscrowDuration = 19;
			ConfirmationDuration = 6;
			AlicePaymentDuration = 38;
			TumblerCashoutDuration = 18;
		}
		public int Start
		{
			get; set;
		}

		public CyclePhaseInformation GetPhaseInformation(int currentHeight)
		{
			if(currentHeight < Start)
				throw new InvalidOperationException("No cycle possible before " + Start);
			int remainingBlocks;
			var phase = GetPhase(currentHeight, out remainingBlocks);
			return new CyclePhaseInformation()
			{
				Phase = phase,
				Cycle = (currentHeight - Start) / GetTumblerLockTimeOffset(),
				RemainingBlock = remainingBlocks,
				Offset = (currentHeight - Start) % GetTumblerLockTimeOffset()
			};
		}

		CyclePhase GetPhase(int currentHeight, out int remainingBlocks)
		{
			var offset = (currentHeight - Start) % GetTumblerLockTimeOffset();
			if(offset < AliceEscrowDuration)
			{
				remainingBlocks = AliceEscrowDuration - offset;
				return CyclePhase.AliceEscrowPhase;
			}
			int nextPhase = AliceEscrowDuration;
			if(offset < nextPhase + TumblerEscrowDuration)
			{
				remainingBlocks = nextPhase + TumblerEscrowDuration - offset;
				return CyclePhase.TumblerEscrowPhase;
			}
			nextPhase += TumblerEscrowDuration;
			if(offset < nextPhase + ConfirmationDuration)
			{
				remainingBlocks = nextPhase + ConfirmationDuration - offset;
				return CyclePhase.TumblerEscrowConfirmation;
			}
			nextPhase += ConfirmationDuration;
			if(offset < nextPhase + AlicePaymentDuration)
			{
				remainingBlocks = nextPhase + AlicePaymentDuration - offset;
				return CyclePhase.AlicePaymentPhase;
			}
			nextPhase += AlicePaymentDuration;
			if(offset < nextPhase + TumblerCashoutDuration)
			{
				remainingBlocks = nextPhase + TumblerCashoutDuration - offset;
				return CyclePhase.TumblerCashoutPhase;
			}
			nextPhase += TumblerCashoutDuration;
			if(offset < nextPhase + BobCashoutDuration)
			{
				remainingBlocks = nextPhase + BobCashoutDuration - offset;
				return CyclePhase.BobCashoutPhase;
			}
			nextPhase += BobCashoutDuration;
			remainingBlocks = nextPhase + ConfirmationDuration - offset;
			return CyclePhase.BobCashoutConfirmation;
		}

		public LockTime GetAliceLockTime(int cycle)
		{
			return Start + cycle * GetTotalDuration() + GetAliceLockTimeOffset();
		}
		public LockTime GetTumblerLockTime(int cycle)
		{
			return Start + cycle * GetTotalDuration() + GetTumblerLockTimeOffset();
		}

		public int ConfirmationDuration
		{
			get; set;
		}

		public int AlicePaymentDuration
		{
			get; set;
		}

		public int TumblerCashoutDuration
		{
			get; set;
		}

		public int BobCashoutDuration
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
				AlicePaymentDuration +
				TumblerCashoutDuration;
		}
		public int GetTumblerLockTimeOffset()
		{
			return GetAliceLockTimeOffset() + BobCashoutDuration + ConfirmationDuration;
		}
		public int GetTotalDuration()
		{
			return GetTumblerLockTimeOffset();
		}
	}
}
