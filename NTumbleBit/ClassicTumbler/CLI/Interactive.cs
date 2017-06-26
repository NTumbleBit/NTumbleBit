using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using CommandLine;
using System.Reflection;
using NBitcoin;
using NTumbleBit.ClassicTumbler;
using System.Threading;
using NTumbleBit.Services;
using NTumbleBit.ClassicTumbler.Client;

namespace NTumbleBit.ClassicTumbler.CLI
{
	public class Interactive : IDisposable
	{
		public Interactive()
		{
		}

		public IInteractiveRuntime Runtime
		{
			get;
			set;
		}	

		public void StartInteractive()
		{
			Console.Write(Assembly.GetEntryAssembly().GetName().Name
						+ " " + Assembly.GetEntryAssembly().GetName().Version);
			Console.WriteLine(" -- TumbleBit Implementation in .NET Core");
			Console.WriteLine("Type \"help\" or \"help <command>\" for more information.");

			bool quit = false;
			while(!quit)
			{
				Console.Write(">>> ");
				var split = Console.ReadLine().Split(null);
				try
				{

					Parser.Default.ParseArguments<StatusOptions, StopOptions, QuitOptions>(split)
						.WithParsed<StatusOptions>(_ => GetStatus(_))
						.WithParsed<StopOptions>(_ => Stop(_))
						.WithParsed<QuitOptions>(_ => quit = true);
				}
				catch(FormatException)
				{
					Console.WriteLine("Invalid format");
				}
			}
		}

		private void Stop(StopOptions opt)
		{
			opt.Target = opt.Target ?? "";
			var stopMixer = opt.Target.Equals("mixer", StringComparison.OrdinalIgnoreCase);
			var stopBroadcasted = opt.Target.Equals("broadcaster", StringComparison.OrdinalIgnoreCase);
			var both = opt.Target.Equals("both", StringComparison.OrdinalIgnoreCase);
			if(both)
				stopMixer = stopBroadcasted = true;
			if(stopMixer)
			{
				_MixingCTS.Cancel();
			}
			if(stopBroadcasted)
			{
				_BroadcasterCTS.Cancel();
			}
			if(!stopMixer && !stopBroadcasted)
				throw new FormatException();
		}

		void GetStatus(StatusOptions options)
		{
			options.Query = options?.Query?.Trim() ?? String.Empty;
			if(!string.IsNullOrWhiteSpace(options.Query))
			{
				bool parsed = false;
				try
				{
					options.CycleId = int.Parse(options.Query);
					parsed = true;
				}
				catch { }
				try
				{
					options.TxId = new uint256(options.Query).ToString();
					parsed = true;
				}
				catch { }
				try
				{
					options.Address = BitcoinAddress.Create(options.Query, Runtime.Network).ToString();
					parsed = true;
				}
				catch { }
				if(!parsed)
					throw new FormatException();
			}

			if(options.CycleId != null)
			{
				CycleParameters cycle = null;

				try
				{
					cycle = Runtime.TumblerParameters?.CycleGenerator?.GetCycle(options.CycleId.Value);
				}
				catch
				{
					Console.WriteLine("Invalid cycle");
					return;
				}
				var records = Runtime.Tracker.GetRecords(options.CycleId.Value);
				var currentHeight = Runtime.Services.BlockExplorerService.GetCurrentHeight();

				StringBuilder builder = new StringBuilder();
				var phases = new[]
				{
					CyclePhase.Registration,
					CyclePhase.ClientChannelEstablishment,
					CyclePhase.TumblerChannelEstablishment,
					CyclePhase.PaymentPhase,
					CyclePhase.TumblerCashoutPhase,
					CyclePhase.ClientCashoutPhase
				};

				if(cycle != null)
				{
					Console.WriteLine("Phases:");
					var periods = cycle.GetPeriods();
					foreach(var phase in phases)
					{
						var period = periods.GetPeriod(phase);
						if(period.IsInPeriod(currentHeight))
							builder.Append("(");
						builder.Append(phase.ToString());
						if(period.IsInPeriod(currentHeight))
							builder.Append($" {(period.End - currentHeight)} blocks left)");

						if(phase != CyclePhase.ClientCashoutPhase)
							builder.Append("-");
					}
					Console.WriteLine(builder.ToString());
					Console.WriteLine();
				}

				Console.WriteLine("Records:");
				foreach(var correlationGroup in records.GroupBy(r => r.Correlation).OrderBy(o => (int)o.Key))
				{
					Console.WriteLine("========");
					foreach(var group in correlationGroup.GroupBy(r => r.TransactionType).OrderBy(o => (int)o.Key))
					{
						builder = new StringBuilder();
						builder.AppendLine(group.Key.ToString());
						foreach(var data in group.OrderBy(g => g.RecordType))
						{
							builder.Append("\t" + data.RecordType.ToString());
							if(data.ScriptPubKey != null)
								builder.AppendLine(" " + data.ScriptPubKey.GetDestinationAddress(Runtime.Network));
							if(data.TransactionId != null)
								builder.AppendLine(" " + data.TransactionId);
						}
						Console.WriteLine(builder.ToString());
					}
					Console.WriteLine("========");
				}
			}

			if(options.TxId != null)
			{
				var currentHeight = Runtime.Services.BlockExplorerService.GetCurrentHeight();
				var txId = new uint256(options.TxId);
				var result = Runtime.Tracker.Search(txId);
				foreach(var record in result)
				{
					Console.WriteLine("Cycle " + record.Cycle);
					Console.WriteLine("Type " + record.TransactionType);
				}

				var knownTransaction = Runtime.Services.TrustedBroadcastService.GetKnownTransaction(txId);
				Transaction tx = knownTransaction?.Transaction;
				if(knownTransaction != null)
				{
					if(knownTransaction.BroadcastableHeight != 0)
					{
						var blockLeft = (knownTransaction.BroadcastableHeight - currentHeight);
						Console.Write("Planned for " + knownTransaction.BroadcastableHeight.ToString());
						if(blockLeft > 0)
							Console.WriteLine($" ({blockLeft} blocks left)");
						else
							Console.WriteLine($" ({-blockLeft} blocks ago)");
					}
				}
				if(tx == null)
				{
					tx = Runtime.Services.BroadcastService.GetKnownTransaction(txId);
				}
				var txInfo = Runtime.Services.BlockExplorerService.GetTransaction(txId);
				if(tx == null)
					tx = txInfo?.Transaction;
				if(txInfo != null)
				{
					if(txInfo.Confirmations != 0)
						Console.WriteLine(txInfo.Confirmations + " Confirmations");
					else
						Console.WriteLine("Unconfirmed");
				}

				if(tx != null)
				{
					Console.WriteLine("Hex " + tx.ToHex());
				}
				//TODO ask to other objects for more info
			}

			if(options.Address != null)
			{
				var address = BitcoinAddress.Create(options.Address, Runtime.TumblerParameters.Network);
				var result = Runtime.Tracker.Search(address.ScriptPubKey);
				foreach(var record in result)
				{
					Console.WriteLine("Cycle " + record.Cycle);
					Console.WriteLine("Type " + record.TransactionType);
				}
				if(Runtime.DestinationWallet != null)
				{
					var keyPath = Runtime.DestinationWallet.GetKeyPath(address.ScriptPubKey);
					if(keyPath != null)
						Console.WriteLine("KeyPath: " + keyPath.ToString());
				}
			}
		}


		CancellationTokenSource _MixingCTS = new CancellationTokenSource();
		CancellationTokenSource _BroadcasterCTS = new CancellationTokenSource();
		public CancellationToken MixingCancellationToken
		{
			get
			{
				return _MixingCTS.Token;
			}
		}

		public CancellationToken BroadcasterCancellationToken
		{
			get
			{
				return _BroadcasterCTS.Token;
			}
		}

		public void Dispose()
		{
			if(!_MixingCTS.IsCancellationRequested)
				_MixingCTS.Cancel();
			if(!_BroadcasterCTS.IsCancellationRequested)
				_BroadcasterCTS.Cancel();
			if(Runtime != null)
				Runtime.Dispose();
		}
	}
}
