using Microsoft.Extensions.Logging;
using NBitcoin;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler.Client
{
	public class StateMachinesExecutor : TumblerServiceBase
	{
		public StateMachinesExecutor(
			TumblerClientRuntime runtime)
		{
			if(runtime == null)
				throw new ArgumentNullException("runtime");
			Runtime = runtime;
		}


		public TumblerClientRuntime Runtime
		{
			get; set;
		}

		public override string Name => "mixer";

		protected override void StartCore(CancellationToken cancellationToken)
		{
			new Thread(() =>
			{
				Logs.Client.LogInformation("State machines started");
				uint256 lastBlock = uint256.Zero;
				int lastCycle = 0;
				while(true)
				{
					Exception unhandled = null;
					try
					{
						lastBlock = Runtime.Services.BlockExplorerService.WaitBlock(lastBlock, cancellationToken);
						var height = Runtime.Services.BlockExplorerService.GetCurrentHeight();
						Logs.Client.LogInformation("New Block: " + height);
						var cycle = Runtime.TumblerParameters.CycleGenerator.GetRegistratingCycle(height);
						if(lastCycle != cycle.Start)
						{
							lastCycle = cycle.Start;
							Logs.Client.LogInformation("New Cycle: " + cycle.Start);
							PaymentStateMachine.State state = GetPaymentStateMachineState(cycle);
							if(state == null)
							{
								var stateMachine = new PaymentStateMachine(Runtime, null);
								Save(stateMachine, cycle.Start);
							}
						}

						var cycles = Runtime.TumblerParameters.CycleGenerator.GetCycles(height);
						foreach(var state in cycles.SelectMany(c => Runtime.Repository.List<PaymentStateMachine.State>(GetPartitionKey(c.Start))))
						{
							var machine = new PaymentStateMachine(Runtime, state);
							try
							{
								machine.Update();
								machine.InvalidPhaseCount = 0;
							}
							catch(PrematureRequestException)
							{
								Logs.Client.LogInformation("Skipping update, need to wait for tor circuit renewal");
								break;
							}
							catch(Exception ex)
							{
								var invalidPhase = ex.Message.IndexOf("invalid-phase", StringComparison.OrdinalIgnoreCase) >= 0;

								if(invalidPhase)
									machine.InvalidPhaseCount++;
								else
									machine.InvalidPhaseCount = 0;

								if(invalidPhase && machine.InvalidPhaseCount > 2)
								{
									Logs.Client.LogError(new EventId(), ex, $"Invalid-Phase happened repeatedly, check that your node currently at height {height} is currently sync to the network");
								}
								else if(!invalidPhase)
								{
									Logs.Client.LogError(new EventId(), ex, "Unhandled StateMachine Error");
								}
							}
							Save(machine, machine.StartCycle);
						}
					}
					catch(OperationCanceledException ex)
					{
						if(cancellationToken.IsCancellationRequested)
						{
							Stopped();
							break;
						}
						else
							unhandled = ex;
					}
					catch(Exception ex)
					{
						unhandled = ex;
					}
					if(unhandled != null)
					{
						Logs.Client.LogError("StateMachineExecutor Error: " + unhandled.ToString());
						cancellationToken.WaitHandle.WaitOne(5000);
					}
				}
			}).Start();
		}

		public PaymentStateMachine.State GetPaymentStateMachineState(CycleParameters cycle)
		{
			return Runtime.Repository.Get<PaymentStateMachine.State>(GetPartitionKey(cycle.Start), "");
		}

		private string GetPartitionKey(int cycle)
		{
			return "Cycle_" + cycle;
		}

		private void Save(PaymentStateMachine stateMachine, int cycle)
		{
			Runtime.Repository.UpdateOrInsert(GetPartitionKey(cycle), "", stateMachine.GetInternalState(), (o, n) => n);
		}
	}
}
