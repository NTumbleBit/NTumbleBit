using CommandLine;

namespace NTumbleBit.ClassicTumbler.CLI
{
	[Verb("status", HelpText = "Shows the current status.")]
	internal class StatusOptions
	{
		[Value(0, HelpText = "Search information about the specifed, cycle/transaction/address. (`status now-1` will show the previous cycle, `status 293212+1` will show the cycle after 293212)")]
		public string Query
		{
			get; set;
		}
		
		public int? CycleId
		{
			get; set;
		}

		[Option('p', "previous", HelpText = "When use with a cycle ID, will show the previous n cycle (example: `cycle 460391 -p 10` will show the last 10 cycle starting from the cycle 460391)")]
		public int PreviousCount
		{
			get; set;
		} = 1;

		public string TxId
		{
			get; set;
		}
		
		public string Address
		{
			get; set;
		}
		public int CycleOffset
		{
			get;
			set;
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