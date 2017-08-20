using NBitcoin;
using System.Linq;
using NBitcoin.Crypto;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace NTumbleBit.ClassicTumbler
{
	public class StandardCycle
	{
		public string FriendlyName
		{
			get; set;
		}

		public Money Denomination
		{
			get; set;
		}

		public OverlappedCycleGenerator Generator
		{
			get; set;
		}
		public Consensus Consensus
		{
			get; set;
		}

		public Money CoinsPerDay()
		{
			var satoshisPerDay = Math.Round((decimal)Denomination.Satoshi / (decimal)GetLength(false).TotalDays);
			return Money.Satoshis(satoshisPerDay);
		}

		public TimeSpan GetLength(bool startup)
		{
			var periods = Generator.FirstCycle.GetPeriods();
			var nonOverlappedPart = periods.Registration.End - periods.Registration.Start - Generator.RegistrationOverlap;
			var overlappedPart = periods.Total.End - periods.Total.Start - nonOverlappedPart;
			return StandardCycles.GetEstimatedTime(Consensus, startup ? nonOverlappedPart + overlappedPart : nonOverlappedPart);
		}
	}
	public class StandardCycles
	{
		bool _Debug;
		public bool Debug
		{
			get
			{
				return _Debug;
			}
		}

		public StandardCycles(Network network):this(network.Consensus, IsDebug(network))
		{

		}

		private static bool IsDebug(Network network)
		{
			return network == Network.TestNet || network == Network.RegTest;
		}

		public StandardCycles(Consensus consensus, bool debug)
		{
			_Debug = debug;

			_Shorty = new StandardCycle()
			{
				FriendlyName = "Shorty",
				Consensus = consensus,
				Denomination = Money.Coins(1.0m),
				Generator = new OverlappedCycleGenerator()
				{
					RegistrationOverlap = 1,
					FirstCycle = new CycleParameters()
					{
						Start = 1,
						RegistrationDuration = GetBlocksCount(consensus, 20) + 1,
						SafetyPeriodDuration = GetBlocksCount(consensus, 10),
						ClientChannelEstablishmentDuration = GetBlocksCount(consensus, 20),
						TumblerChannelEstablishmentDuration = GetBlocksCount(consensus, 20),
						PaymentPhaseDuration = GetBlocksCount(consensus, 20),
						TumblerCashoutDuration = GetBlocksCount(consensus, 40),
						ClientCashoutDuration = GetBlocksCount(consensus, 20),
					}
				}
			};

			_Shorty2x = new StandardCycle()
			{
				FriendlyName = "Shorty2x",
				Consensus = consensus,
				Denomination = Money.Coins(2.0m),
				Generator = new OverlappedCycleGenerator()
				{
					RegistrationOverlap = 1,
					FirstCycle = new CycleParameters()
					{
						Start = 1,
						RegistrationDuration = GetBlocksCount(consensus, 20 * 2) + 1,
						SafetyPeriodDuration = GetBlocksCount(consensus, 10),
						ClientChannelEstablishmentDuration = GetBlocksCount(consensus, 20 * 4),
						TumblerChannelEstablishmentDuration = GetBlocksCount(consensus, 20 * 4),
						PaymentPhaseDuration = GetBlocksCount(consensus, 20 * 2),
						TumblerCashoutDuration = GetBlocksCount(consensus, 40 * 2),
						ClientCashoutDuration = GetBlocksCount(consensus, 20 * 2),
					}
				}
			};

			_Kotori = new StandardCycle()
			{
				FriendlyName = "Kotori",
				Consensus = consensus,
				Denomination = Money.Coins(1.0m),
				Generator = new OverlappedCycleGenerator()
				{
					RegistrationOverlap = 1,
					FirstCycle = new CycleParameters()
					{
						Start = 0,
						//one cycle per day
						RegistrationDuration = GetBlocksCount(consensus, 60 * 4) + 1,
						//make sure tor circuit get renewed
						SafetyPeriodDuration = GetBlocksCount(consensus, 20),
						ClientChannelEstablishmentDuration = GetBlocksCount(consensus, 120),
						TumblerChannelEstablishmentDuration = GetBlocksCount(consensus, 120),
						PaymentPhaseDuration = GetBlocksCount(consensus, 30),
						TumblerCashoutDuration = GetBlocksCount(consensus, 5 * 60),
						ClientCashoutDuration = GetBlocksCount(consensus, 5 * 60)
					}
				}
			};


			if(!_Debug)
			{
				
				//Verify that 2 phases are always at least separated by 20 minutes
				foreach(var standard in ToEnumerable())
				{
					HashSet<uint256> states = new HashSet<uint256>();

					var start = standard.Generator.FirstCycle.Start;
					var periods = standard.Generator.FirstCycle.GetPeriods();
					var nonOverlappedPart = periods.Registration.End - periods.Registration.Start - standard.Generator.RegistrationOverlap;
					var total = periods.Total.End - periods.Total.Start;
					

					var maxOverlapped = Math.Ceiling((decimal)total / (decimal)nonOverlappedPart);

					for(int i = start;; i += nonOverlappedPart)
					{
						var starts = 
							standard.Generator.GetCycles(i)
							.SelectMany(c =>
							{
								var p = c.GetPeriods();
								return new[]
								{
									p.Registration.Start,
									p.ClientChannelEstablishment.Start,
									p.TumblerChannelEstablishment.Start,
									p.TumblerCashout.Start,
									p.ClientCashout.Start
								};
							}).OrderBy(c => c).ToArray();
						for(int ii = 1; ii < starts.Length; ii++)
						{
							if(starts[ii] - starts[ii - 1] < GetBlocksCount(consensus, 20))
								throw new InvalidOperationException("A standard cycle generator generates cycles which overlap too much");
						}

						//Check if it is a we already checked such state module total
						for(int ii = 0; ii < starts.Length; ii++)
						{
							starts[ii] = starts[ii] % total;
						}
						MemoryStream ms = new MemoryStream();
						BitcoinStream bs = new BitcoinStream(ms, true);
						bs.ReadWrite(ref starts);
						if(!states.Add(Hashes.Hash256(ms.ToArray())))
							break;
					}
				}
			}
		}

		internal static int GetBlocksCount(Consensus consensus, int minutes)
		{
			return (int)Math.Ceiling((double)TimeSpan.FromMinutes(minutes).Ticks / consensus.PowTargetSpacing.Ticks);
		}
		internal static TimeSpan GetEstimatedTime(Consensus consensus, int blocks)
		{
			return TimeSpan.FromTicks(consensus.PowTargetSpacing.Ticks * blocks);
		}

		StandardCycle _Kotori;
		public StandardCycle Kotori
		{
			get
			{
				return _Kotori;
			}
		}


		private readonly StandardCycle _Shorty2x;
		public StandardCycle Shorty2x
		{
			get
			{
				return _Shorty2x;
			}
		}

		StandardCycle _Shorty;
		public StandardCycle Shorty
		{
			get
			{
				return _Shorty;
			}
		}

		public IEnumerable<StandardCycle> ToEnumerable()
		{
			yield return _Kotori;
			if(_Debug)
			{
				yield return _Shorty;
				yield return _Shorty2x;
			}
		}

		public StandardCycle GetStandardCycle(ClassicTumblerParameters tumblerParameters)
		{
			return ToEnumerable().FirstOrDefault(c => c.Generator.GetHash() == tumblerParameters.CycleGenerator.GetHash() && c.Denomination == tumblerParameters.Denomination);
		}

		public StandardCycle GetStandardCycle(string name)
		{
			return ToEnumerable().FirstOrDefault(c => c.FriendlyName.Equals(name, StringComparison.OrdinalIgnoreCase));
		}
	}
}
