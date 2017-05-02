using CommandLine;

namespace NTumbleBit.CLI
{
	[Verb("tumble", HelpText = "Start a tumbler.")]
	internal class TumbleOptions
	{
		//normal options here
	}

	[Verb("status", HelpText = "Shows the current status.")]
	internal class StatusOptions
	{
		//normal options here
	}

	[Verb("quit", HelpText = "Quit.")]
	internal class QuitOptions
	{
		//normal options here
	}
}