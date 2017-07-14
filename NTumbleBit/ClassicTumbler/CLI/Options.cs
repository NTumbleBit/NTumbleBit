using CommandLine;

namespace NTumbleBit.ClassicTumbler.CLI
{
	[Verb("status", HelpText = "Shows the current status.")]
	internal class StatusOptions
	{
		[Value(0, HelpText = "Search information about the specifed, cycle/transaction/address.")]
		public string Query
		{
			get; set;
		}
		
		public int? CycleId
		{
			get; set;
		}

		public string TxId
		{
			get; set;
		}
		
		public string Address
		{
			get; set;
		}
	}

	[Verb("services", HelpText = "Manage services (Example: services start all)")]
	internal class ServicesOptions
	{
		[Value(0, HelpText = "start, stop or list")]
		public string Action
		{
			get; set;
		}
		[Value(1, HelpText = "Service names separated by commas ('all' for selecting all)")]
		public string Target
		{
			get; set;
		}
	}

	[Verb("start", HelpText = "Start a service.")]
	internal class StartOptions
	{
		[Value(0, HelpText = "Start a service")]
		public string Target
		{
			get; set;
		}
	}

	[Verb("exit", HelpText = "Quit.")]
	internal class QuitOptions
	{
		//normal options here
	}
}