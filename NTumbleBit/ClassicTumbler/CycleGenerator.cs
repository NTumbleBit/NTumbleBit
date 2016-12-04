using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
    public class OverlappedCycleGenerator
    {
		public OverlappedCycleGenerator()
		{
			FirstCycle = new CycleParameters();
			RegistrationOverlap = 1;
		}

		public int RegistrationOverlap
		{
			get; set;
		}

		public CycleParameters FirstCycle
		{
			get; set;
		}

		public CycleParameters GetRegistratingCycle(int blockHeight)
		{
			if(blockHeight < FirstCycle.Start)
				throw new InvalidOperationException("cycle generation starts at " + FirstCycle.Start);

			var registrationLength = FirstCycle.RegistrationDuration - RegistrationOverlap;
			var cycleCount = (blockHeight - FirstCycle.Start) / registrationLength;

			var cycle = FirstCycle.Clone();
			cycle.Start += registrationLength * cycleCount;
			return cycle;
		}
	}
}
