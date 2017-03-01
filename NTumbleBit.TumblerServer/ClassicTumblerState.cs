using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using NTumbleBit.TumblerServer;
using NTumbleBit.TumblerServer.Services;
using System;
using System.Collections.Generic;

namespace NTumbleBit.ClassicTumbler
{
	public class ClassicTumblerState
	{
		
	}

	public class ClassicTumblerCycle
	{
		public string height;
		public SolverServerSession.State[] solvers;
		public PromiseServerSession.State[] promises;

		public ClassicTumblerCycle(string height, SolverServerSession.State[] solvers, PromiseServerSession.State[] promises)
		{
			this.height = height;
			this.solvers = solvers;
			this.promises = promises;
		}

	}
}