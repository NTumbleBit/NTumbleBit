using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NTumbleBit.Tests
{
    public class TumblerServerTests
    {
		[Fact]
		public void CanGetParameters()
		{
			using(var server = TumblerServerTester.Create())
			{
				var client = server.CreateTumblerClient();
				var promise = client.GetPromiseParametersAsync().GetAwaiter().GetResult();
				Assert.NotNull(promise.ServerKey);
				Assert.NotEqual(0, promise.RealTransactionCount);
				Assert.NotEqual(0, promise.FakeTransactionCount);
				Assert.NotNull(promise.FakeFormat);

				var solver = client.GetSolverParametersAsync().GetAwaiter().GetResult();
				Assert.NotNull(solver.ServerKey);
				Assert.NotEqual(0, solver.FakePuzzleCount);
				Assert.NotEqual(0, solver.RealPuzzleCount);
			}
		}
    }
}
