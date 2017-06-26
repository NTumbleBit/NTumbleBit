using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using CommandLine;
using System.Reflection;
using NBitcoin;
using NTumbleBit.ClassicTumbler;
using System.Threading;
#if !CLIENT
using NTumbleBit.TumblerServer.Services;
using NTumbleBit.TumblerServer;
#else
using NTumbleBit.Client.Tumbler.Services;
using NTumbleBit.Client.Tumbler;
#endif


#if !CLIENT
namespace NTumbleBit.TumblerServer
#else
namespace NTumbleBit.CLI
#endif
{
	public partial class Program
	{
		public ClassicTumblerParameters TumblerParameters
		{
			get; set;
		}
		public Network Network
		{
			get; set;
		}
		public Tracker Tracker
		{
			get; set;
		}
		public ExternalServices Services
		{
			get; set;
		}
		public CancellationTokenSource MixingToken
		{
			get; set;
		}
		public CancellationTokenSource BroadcasterToken
		{
			get; set;
		}

#if CLIENT
		public IDestinationWallet DestinationWallet
		{
			get; set;
		}
#endif

		void StartInteractive()
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
			MixingToken.Cancel();
			BroadcasterToken.Cancel();
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
				MixingToken.Cancel();
			}
			if(stopBroadcasted)
			{
				BroadcasterToken.Cancel();
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
					options.Address = BitcoinAddress.Create(options.Query, Network).ToString();
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
					cycle = TumblerParameters?.CycleGenerator?.GetCycle(options.CycleId.Value);
				}
				catch
				{
					Console.WriteLine("Invalid cycle");
					return;
				}
				var records = Tracker.GetRecords(options.CycleId.Value);
				var currentHeight = Services.BlockExplorerService.GetCurrentHeight();

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
								builder.AppendLine(" " + data.ScriptPubKey.GetDestinationAddress(Network));
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
				var currentHeight = Services.BlockExplorerService.GetCurrentHeight();
				var txId = new uint256(options.TxId);
				var result = Tracker.Search(txId);
				foreach(var record in result)
				{
					Console.WriteLine("Cycle " + record.Cycle);
					Console.WriteLine("Type " + record.TransactionType);
				}

				var knownTransaction = Services.TrustedBroadcastService.GetKnownTransaction(txId);
				Transaction tx = knownTransaction?.Transaction;
				if(knownTransaction != null)
				{
					if(knownTransaction.BroadcastableHeight != 0)
					{
						var blockLeft = (knownTransaction.BroadcastableHeight - currentHeight);
						Console.Write("Planned for " + knownTransaction.BroadcastableHeight.ToString());
						if(blockLeft > 0)
							Console.Write($"({blockLeft} blocks left)");
						else
							Console.Write($"({-blockLeft} blocks ago)");
					}
				}
				if(tx == null)
				{
					tx = Services.BroadcastService.GetKnownTransaction(txId);
				}
				var txInfo = Services.BlockExplorerService.GetTransaction(txId);
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
				var address = BitcoinAddress.Create(options.Address, TumblerParameters.Network);
				var result = Tracker.Search(address.ScriptPubKey);
				foreach(var record in result)
				{
					Console.WriteLine("Cycle " + record.Cycle);
					Console.WriteLine("Type " + record.TransactionType);
				}
#if CLIENT
				if(DestinationWallet != null)
				{
					var keyPath = DestinationWallet.GetKeyPath(address.ScriptPubKey);
					if(keyPath != null)
						Console.WriteLine("KeyPath: " + keyPath.ToString());
				}
#endif
			}
		}
	}
}
