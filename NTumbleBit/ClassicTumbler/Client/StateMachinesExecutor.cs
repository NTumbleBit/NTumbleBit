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
	public class StateMachinesExecutor
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
		
		private CancellationToken _Stop;
		public void Start(CancellationToken cancellation)
		{
			_Stop = cancellation;
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
						lastBlock = Runtime.Services.BlockExplorerService.WaitBlock(lastBlock, _Stop);
						var height = Runtime.Services.BlockExplorerService.GetCurrentHeight();
						Logs.Client.LogInformation("New block of height " + height);
						var cycle = Runtime.TumblerParameters.CycleGenerator.GetRegistratingCycle(height);
						if(lastCycle != cycle.Start)
						{
							lastCycle = cycle.Start;
							Logs.Client.LogInformation("New registering cycle " + cycle.Start);

							var state = Runtime.Repository.Get<PaymentStateMachine.State>(GetPartitionKey(cycle.Start), cycle.Start.ToString());
							if(state == null)
							{
								var stateMachine = CreateStateMachine(null);
								Save(stateMachine, cycle.Start);
								Logs.Client.LogInformation("New state machine created");
							}
						}

						var cycles = Runtime.TumblerParameters.CycleGenerator.GetCycles(height);
						foreach(var state in cycles.SelectMany(c => Runtime.Repository.List<PaymentStateMachine.State>(GetPartitionKey(c.Start))))
						{
							var machine = CreateStateMachine(state);
							try
							{
								machine.Update();
								machine.InvalidPhaseCount = 0;
							}
							catch(Exception ex)
							{
								var invalidPhase = ex.Message.IndexOf("invalid-phase", StringComparison.OrdinalIgnoreCase) >= 0;

								if(invalidPhase)
									machine.InvalidPhaseCount++;
								else
									machine.InvalidPhaseCount = 0;

								if(!invalidPhase || machine.InvalidPhaseCount > 2)
								{
									Logs.Client.LogError("Error while executing state machine " + machine.StartCycle + ": " + ex.ToString());
								}

							}
							Save(machine, machine.StartCycle);
						}
					}
					catch(OperationCanceledException ex)
					{
						if(_Stop.IsCancellationRequested)
						{
							Logs.Client.LogInformation("Mixer stopped");
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
						Logs.Client.LogError("Uncaught exception StateMachineExecutor : " + unhandled.ToString());
						_Stop.WaitHandle.WaitOne(5000);
					}
				}
			}).Start();
		}

		private string GetPartitionKey(int cycle)
		{
			return "Cycle_" + cycle;
		}
		private void Save(PaymentStateMachine stateMachine, int cycle)
		{
			Runtime.Repository.UpdateOrInsert(GetPartitionKey(cycle), "", stateMachine.GetInternalState(), (o, n) => n);
		}

		public PaymentStateMachine CreateStateMachine(PaymentStateMachine.State state)
		{
			return new PaymentStateMachine(Runtime, state);
		}
	}
}
