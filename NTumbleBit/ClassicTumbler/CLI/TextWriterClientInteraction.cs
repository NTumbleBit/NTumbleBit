using NTumbleBit.ClassicTumbler;
using NBitcoin;
using NTumbleBit.ClassicTumbler.CLI;
using NTumbleBit.ClassicTumbler.Client.ConnectionSettings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class TextWriterClientInteraction : ClientInteraction
	{
		TextReader _Input;
		TextWriter _Output;
		public TextWriterClientInteraction(TextWriter output, TextReader input)
		{
			if(input == null)
				throw new ArgumentNullException("input");
			if(output == null)
				throw new ArgumentNullException("output");
			_Input = input;
			_Output = output;
		}

		public Task AskConnectToTorAsync(string torPath, string args)
		{
			_Output.WriteLine("------");
			_Output.WriteLine($"Unable to connect to tor control port, trying to run Tor with the following command from torpath settings, do you accept? (type 'yes' to accept)");
			_Output.WriteLine("------");
			_Output.WriteLine($"{torPath} {args}");
			_Output.WriteLine("------");
			var response = _Input.ReadLine();
			if(!response.Equals("yes", StringComparison.OrdinalIgnoreCase))
				throw new ClientInteractionException("User refused to start Tor");
			return Task.CompletedTask;
		}

		public Task ConfirmParametersAsync(ClassicTumblerParameters parameters, StandardCycle standardCyle)
		{
			var feeRate = ((decimal)parameters.Fee.Satoshi / (decimal)parameters.Denomination.Satoshi) * 100.0m;
			if(standardCyle == null)
			{
				_Output.WriteLine("------");
				_Output.WriteLine("Do you confirm the following non standard tumbler settings? (type 'yes' to accept)");
				_Output.WriteLine("------");
				_Output.WriteLine(parameters.PrettyPrint());
				_Output.WriteLine("--");
				_Output.WriteLine("Tumbler Fee: " + parameters.Fee.ToString() + $" ({feeRate.ToString("0.00")}%)");
				_Output.WriteLine("Denomination: " + parameters.Denomination.ToString());
				var periods = parameters.CycleGenerator.FirstCycle.GetPeriods();
				_Output.WriteLine("Total cycle length: " + (periods.Total.End - periods.Total.Start) + " blocks");
				_Output.WriteLine("------");
				_Output.WriteLine("Do you confirm the following non standard tumbler settings? (type 'yes' to accept)");
			}
			else
			{
				_Output.WriteLine("------");
				_Output.WriteLine("Do you confirm the following standard tumbler settings? (type 'yes' to accept)");
				_Output.WriteLine("------");
				_Output.WriteLine($"Well-known cycle generator: {standardCyle.FriendlyName}");
				_Output.WriteLine("Tumbler Fee: " + parameters.Fee.ToString() + $" ({feeRate.ToString("0.00")}%)");
				_Output.WriteLine("Denomination: " + parameters.Denomination.ToString());
				_Output.WriteLine("Time to tumble the first coin: " + PrettyPrint(standardCyle.GetLength(true)));
				_Output.WriteLine("Time to tumble the following coins: " + PrettyPrint(standardCyle.GetLength(false)));
				_Output.WriteLine($"Peak amount tumbled per day: {standardCyle.CoinsPerDay().ToDecimal(NBitcoin.MoneyUnit.BTC).ToString("0.00")} BTC");
			}
			var response = _Input.ReadLine();
			if(!response.Equals("yes", StringComparison.OrdinalIgnoreCase))
				throw new ClientInteractionException("User refused to confirm the parameters");
			return Task.CompletedTask;
		}

		private string PrettyPrint(TimeSpan t)
		{
			return string.Format("{0:D2}d:{1:D2}h:{2:D2}m",
						t.Days,
						t.Hours,
						t.Minutes);
		}
	}
}
