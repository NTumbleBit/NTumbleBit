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
using System.Threading.Tasks;
using CommandLine.Text;

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


		private readonly List<TumblerServiceBase> _Services = new List<TumblerServiceBase>();
		public List<TumblerServiceBase> Services
		{
			get
			{
				return _Services;
			}
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
				var split = InterruptableConsole.ReadLine().Split(null);
				try
				{

					Parser.Default.ParseArguments<StatusOptions, ServicesOptions, QuitOptions>(split)
						.WithParsed<StatusOptions>(_ => GetStatus(_))
						.WithParsed<ServicesOptions>(_ => ProcessServices(_))
						.WithParsed<QuitOptions>(_ =>
						{
							StopAll();
							quit = true;
						});
				}
				catch(InterruptedConsoleException)
				{
					StopAll();
					throw;
				}
				catch(FormatException)
				{
					Console.WriteLine("Invalid format");
					Parser.Default.ParseArguments<StatusOptions, ServicesOptions, QuitOptions>(new[] { "help", split[0] });
				}
			}
		}

		private void StopAll()
		{
			ProcessServices(new ServicesOptions() { Action = "stop", Target = "all" });
		}

		private void ProcessServices(ServicesOptions opt)
		{
			if(Services.Count == 0)
				return;
			if(opt.Action == null)
				throw new FormatException();
			opt.Target = opt.Target ?? "";
			if(opt.Action.Equals("list", StringComparison.OrdinalIgnoreCase))
				opt.Target = "all";
			var stops = new HashSet<string>(opt.Target.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
			if(stops.Contains("both", StringComparer.OrdinalIgnoreCase))
			{
				//Legacy
				stops.Add("mixer");
				stops.Add("broadcaster");
			}

			var services = Services.Where(s => stops.Contains(s.Name) || stops.Contains("all")).ToList();
			if(services.Count == 0)
			{
				Console.WriteLine("Valid services are " +
					String.Join(",", new[] { "all" }.Concat(Services.Select(c => c.Name)).ToArray()));
				throw new FormatException();
			}

			if(opt.Action.Equals("start", StringComparison.OrdinalIgnoreCase))
			{
				foreach(var service in services.Where(s => !s.Started))
					service.Start();
			}
			else if(opt.Action.Equals("stop", StringComparison.OrdinalIgnoreCase))
			{
				object l = new object();
				var stoppingServices = services.Where(s => s.Started).Select(c => c.Stop().ContinueWith(t =>
				{
					lock(l)
					{
						Console.WriteLine(c.Name + " stopped");
					}
				})).ToArray();
				Task.WaitAll(stoppingServices);
			}
			else if(opt.Action.Equals("list", StringComparison.OrdinalIgnoreCase))
			{
				foreach(var item in services)
				{
					var state = item.Started ? ("started") : ("stopped");
					Console.WriteLine($"Service {item.Name} is {state}");
				}
			}
			else
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
					Console.WriteLine(cycle.ToString(currentHeight));
					var periods = cycle.GetPeriods();
					foreach(var phase in phases)
					{
						var period = periods.GetPeriod(phase);
						if(period.IsInPeriod(currentHeight))
							Console.WriteLine($"In phase: {phase.ToString()}  ({(period.End - currentHeight)} blocks left)");
					}
					Console.WriteLine();
				}

				Console.WriteLine("Records:");
				foreach(var correlationGroup in records.GroupBy(r => r.Correlation))
				{
					Console.WriteLine("========");
					foreach(var group in correlationGroup.GroupBy(r => r.TransactionType).OrderBy(r => (int)r.Key))
					{
						var builder = new StringBuilder();
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



		public void Dispose()
		{
			StopAll();
			if(Runtime != null)
				Runtime.Dispose();
		}
	}
}
