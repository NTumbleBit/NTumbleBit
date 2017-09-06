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
			_ParametersHash = Runtime.TumblerParameters.GetHash();
		}

		uint160 _ParametersHash;
		public TumblerClientRuntime Runtime
		{
			get; set;
		}

		public override string Name => "mixer";

		public int InvalidPhaseCount
		{
			get;
			private set;
		}

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
						var machineStates = cycles
												.SelectMany(c => Runtime.Repository.List<PaymentStateMachine.State>(GetPartitionKey(c.Start)))
												.Where(m => m.TumblerParametersHash == _ParametersHash)
												.ToArray();
						NBitcoin.Utils.Shuffle(machineStates);
						bool hadInvalidPhase = false;

						//Waiting for the block to propagate to server so invalid-phase happens less often
						cancellationToken.WaitHandle.WaitOne(10000);
						cancellationToken.ThrowIfCancellationRequested();

						foreach(var state in machineStates)
						{
							bool noSave = false;
							var machine = new PaymentStateMachine(Runtime, state);
							if(machine.Status == PaymentStateMachineStatus.Wasted)
							{
								Logs.Client.LogDebug($"Skipping cycle {machine.StartCycle}, because if is wasted");
								continue;
							}

							var statusBefore = machine.GetInternalState();
							try
							{
								machine.Update();
								InvalidPhaseCount = 0;
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
								{
									if(!hadInvalidPhase)
									{
										hadInvalidPhase = true;
										InvalidPhaseCount++;
										if(InvalidPhaseCount > 2)
										{
											Logs.Client.LogError(new EventId(), ex, $"Invalid-Phase happened repeatedly, check that your node currently at height {height} is currently sync to the network");
										}
									}
									noSave = true;
								}
								else
								{
									Logs.Client.LogError(new EventId(), ex, "Unhandled StateMachine Error");
								}
							}
							if(!noSave)
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
			var state = Runtime.Repository.Get<PaymentStateMachine.State>(GetPartitionKey(cycle.Start), "");
			if(state == null)
				return null;
			return state.TumblerParametersHash == _ParametersHash ? state : null;
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
