using NBitcoin;
using NTumbleBit.ClassicTumbler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
				var parameters = client.GetTumblerParameters();
				Assert.NotNull(parameters.ServerKey);
				Assert.NotEqual(0, parameters.RealTransactionCount);
				Assert.NotEqual(0, parameters.FakeTransactionCount);
				Assert.NotNull(parameters.FakeFormat);
				Assert.True(parameters.FakeFormat != uint256.Zero);
			}
		}

		[Fact]
		public void CanCompleteCycle()
		{
			using(var server = TumblerServerTester.Create())
			{
				server.BobNode.FindBlock(1);
				server.TumblerNode.FindBlock(1);
				server.AliceNode.FindBlock(105);

				var bobRPC = server.BobNode.CreateRPCClient();

				var client = server.CreateTumblerClient();
				var parameters = client.GetTumblerParameters();

				var height = bobRPC.GetBlockCount() - 1;
				var phaseInfo = parameters.CycleParameters.GetPhaseInformation(height);

				var clientSession = new TumblerClientSession(parameters, phaseInfo.Cycle);
				var voucher = client.AskUnsignedVoucher(0);
			}
		}
	}
}
