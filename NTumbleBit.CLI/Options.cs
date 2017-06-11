using CommandLine;

#if !CLIENT
namespace NTumbleBit.TumblerServer
#else
namespace NTumbleBit.CLI
#endif
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

	[Verb("stop", HelpText = "Stop a service.")]
	internal class StopOptions
	{
		[Value(0, HelpText = "\"stop mixer\" to stop the mixer, \"stop broadcaster\" to stop the broadcaster, \"stop both\" to stop both.")]
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