using CommandLine;

namespace NTumbleBit.CLI
{	
	[Verb("status", HelpText = "Shows the current status.")]
	internal class StatusOptions
	{
		[Option('c', "cycle",
			HelpText = "Query cycle information")]
		public int? CycleId
		{
			get; set;
		}

		[Option('t', "tx",
			HelpText = "Query transaction information")]
		public string TxId
		{
			get; set;
		}

		[Option('a', "address",
			HelpText = "Query address information")]
		public string Address
		{
			get; set;
		}
	}

	[Verb("quit", HelpText = "Quit.")]
	internal class QuitOptions
	{
		//normal options here
	}
}