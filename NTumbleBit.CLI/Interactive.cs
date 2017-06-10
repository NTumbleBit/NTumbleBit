using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using CommandLine;
using System.Reflection;
using NBitcoin;
using NTumbleBit.ClassicTumbler;

namespace NTumbleBit.CLI
{
	public partial class Program
	{
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

					Parser.Default.ParseArguments<StatusOptions, QuitOptions>(split)
						.WithParsed<StatusOptions>(_ => GetStatus(_))
						.WithParsed<QuitOptions>(_ => quit = true);
				}
				catch(FormatException)
				{
					Console.WriteLine("Invalid format");
				}
			}
		}
		void GetStatus(StatusOptions options)
		{
			if(options.CycleId != null)
			{
				CycleParameters cycle = null;

				try
				{
					cycle = TumblerConfiguration.CycleGenerator.GetCycle(options.CycleId.Value);
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
				Console.WriteLine("Records:");
				foreach(var group in records.GroupBy(r => r.TransactionType).OrderBy(o => (int)o.Key))
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
			}

			if(options.TxId != null)
			{
				var txId = new uint256(options.TxId);
				var result = Tracker.Search(txId);
				if(result == null)
					Console.WriteLine("Not found");
				else
				{
					Console.WriteLine("Cycle " + result.Cycle);
					Console.WriteLine("Type " + result.TransactionType);
				}
				//TODO ask to other objects for more info
			}

			if(options.Address != null)
			{
				var address = BitcoinAddress.Create(options.Address, Network);
				var result = Tracker.Search(address.ScriptPubKey);
				if(result == null)
					Console.WriteLine("Not found");
				else
				{
					Console.WriteLine("Cycle " + result.Cycle);
					Console.WriteLine("Type " + result.TransactionType);
				}
				//TODO ask to other objects for more info
			}
		}
	}
}
