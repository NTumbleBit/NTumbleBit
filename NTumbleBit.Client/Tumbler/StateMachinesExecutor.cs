using Microsoft.Extensions.Logging;
using NBitcoin;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.Client.Tumbler.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NTumbleBit.Client.Tumbler
{
	public class StateMachinesExecutor
	{
		public StateMachinesExecutor(
			ClassicTumblerParameters parameters,
			TumblerClient client,
			IDestinationWallet destinationWallet,
			ExternalServices services,
			IRepository repository,
			ILogger logger,
			Tracker tracker)
		{
			Parameters = parameters;
			AliceClient = client;
			BobClient = client;
			Services = services;
			DestinationWallet = destinationWallet;
			Logger = logger;
			Repository = repository;
			Tracker = tracker;
		}

		public Tracker Tracker
		{
			get; set;
		}
		public IRepository Repository
		{
			get; set;
		}

		public ILogger Logger
		{
			get; set;
		}

		public ExternalServices Services
		{
			get; set;
		}
		public TumblerClient BobClient
		{
			get; set;
		}
		public TumblerClient AliceClient
		{
			get; set;
		}
		public ClassicTumblerParameters Parameters
		{
			get; set;
		}
		public IDestinationWallet DestinationWallet
		{
			get;
			private set;
		}
		public bool Cooperative
		{
			get;
			set;
		}

		private CancellationToken _Stop;
		public void Start(CancellationToken cancellation)
		{
			_Stop = cancellation;
			new Thread(() =>
			{

				uint256 lastBlock = uint256.Zero;
				int lastCycle = 0;
				while(true)
				{
					Exception unhandled = null;
					try
					{
						lastBlock = Services.BlockExplorerService.WaitBlock(lastBlock, _Stop);
						var height = Services.BlockExplorerService.GetCurrentHeight();
						Logger.LogInformation("New block of height " + height);
						var cycle = Parameters.CycleGenerator.GetRegistratingCycle(height);
						if(lastCycle != cycle.Start)
						{
							lastCycle = cycle.Start;
							Logger.LogInformation("New registering cycle " + cycle.Start);

							var state = Repository.Get<PaymentStateMachine.State>(GetPartitionKey(cycle.Start), cycle.Start.ToString());
							if(state == null)
							{
								var stateMachine = CreateStateMachine(null);
								Save(stateMachine, cycle.Start);
								Logger.LogInformation("New state machine created");
							}
						}

						var cycles = Parameters.CycleGenerator.GetCycles(height);
						foreach(var state in cycles.SelectMany(c => Repository.List<PaymentStateMachine.State>(GetPartitionKey(c.Start))))
						{
							var machine = CreateStateMachine(state);
							try
							{
								machine.Update(Logger);
							}
							catch(Exception ex)
							{
								var invalidPhase = ex.Message.IndexOf("invalid-phase", StringComparison.OrdinalIgnoreCase) >= 0;

								if(invalidPhase)
									machine.InvalidPhaseCount++;

								if(!invalidPhase || machine.InvalidPhaseCount > 2)
								{
									Logger.LogError("Error while executing state machine " + machine.StartCycle + ": " + ex.ToString());
								}

							}
							Save(machine, machine.StartCycle);
						}
					}
					catch(OperationCanceledException ex)
					{
						if(_Stop.IsCancellationRequested)
						{
							Logger.LogInformation("Mixer stopped");
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
						Logger.LogError("Uncaught exception StateMachineExecutor : " + unhandled.ToString());
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
			Repository.UpdateOrInsert(GetPartitionKey(cycle), "", stateMachine.GetInternalState(), (o, n) => n);
		}

		public PaymentStateMachine CreateStateMachine(PaymentStateMachine.State state)
		{
			return new PaymentStateMachine(Parameters, AliceClient, DestinationWallet, Services, state, Tracker) { Cooperative = Cooperative };
		}
	}
}
