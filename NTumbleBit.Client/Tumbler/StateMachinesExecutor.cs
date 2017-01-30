using Microsoft.Extensions.Logging;
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
			ClientDestinationWallet destinationWallet,
			ExternalServices services,
			IRepository repository,
			ILogger logger)
		{
			Parameters = parameters;
			Client = client;
			Services = services;
			DestinationWallet = destinationWallet;
			Logger = logger;
			Repository = repository;
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
		public TumblerClient Client
		{
			get; set;
		}
		public ClassicTumblerParameters Parameters
		{
			get; set;
		}
		public ClientDestinationWallet DestinationWallet
		{
			get;
			private set;
		}

		private CancellationToken _Stop;
		public void Start(CancellationToken cancellation, TorParameters torParameters = null)
		{
			_Stop = cancellation;
			new Thread(() =>
			{
				try
				{
					int lastHeight = 0;
					int lastCycle = 0;
					while(true)
					{
						_Stop.WaitHandle.WaitOne(5000);
						_Stop.ThrowIfCancellationRequested();

						var height = Services.BlockExplorerService.GetCurrentHeight();
						if(height == lastHeight)
							continue;
						lastHeight = height;
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
								if(torParameters != null)
								{
									var torControl = new DotNetTor.ControlPort.Client(torParameters.Host, torParameters.ControlPort,
										torParameters.ControlPortPassword);
									Logger.LogInformation("Changing TOR identity...");
									torControl.ChangeCircuitAsync().Wait();
								}
								machine.Update(Logger);
							}
							catch(Exception ex)
							{
								Logger.LogError("Error while executing state machine " + machine.StartCycle + ": " + ex.Message);
								Logger.LogDebug(ex.StackTrace);
							}
							Save(machine, machine.StartCycle);
						}
					}
				}
				catch(OperationCanceledException) { }
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

		private PaymentStateMachine CreateStateMachine(PaymentStateMachine.State state)
		{
			return new PaymentStateMachine(Parameters, Client, DestinationWallet, Services, state);
		}
	}
}
