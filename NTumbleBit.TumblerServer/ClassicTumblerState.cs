using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using NTumbleBit.TumblerServer;
using NTumbleBit.TumblerServer.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NTumbleBit.ClassicTumbler
{
	public class ClassicTumblerState
	{
		public List<ClassicTumble> Tumbles;
		public string test;
		private IRepository repo;

		public ClassicTumblerState(ClassicTumblerRepository repo)
		{
			this.repo = repo.Repository;
			Tumbles = new List<ClassicTumble>();
			List<string> keys = this.repo.ListPartitionKeys().Where(x => x.Contains("Cycle")).ToList();
			foreach (var key in keys)
			{
				Tumbles.Add(new ClassicTumble(
					key.Substring(key.LastIndexOf("_") + 1),
					this.repo.List<SolverServerSession.State>(key),
					this.repo.List<PromiseServerSession.State>(key)
				));
			}
		}
	}

	public class ClassicTumble
	{
		public string height;
		public List<SolverServerSession.State> solvers;
		public List<PromiseServerSession.State> promises;

		public ClassicTumble(string height, List<SolverServerSession.State> solvers, List<PromiseServerSession.State> promises)
		{
			this.height = height;
			this.solvers = solvers;
			this.promises = promises;
		}

	}
}