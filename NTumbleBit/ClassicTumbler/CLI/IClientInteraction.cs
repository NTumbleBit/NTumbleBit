using NTumbleBit.ClassicTumbler.Client.ConnectionSettings;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler.CLI
{
	public interface ClientInteraction
	{
		Task ConfirmParametersAsync(ClassicTumblerParameters parameters, StandardCycle standardCyle);
		Task AskConnectToTorAsync(string torPath, string args);
	}

	public class AcceptAllClientInteraction : ClientInteraction
	{
		public Task AskConnectToTorAsync(string torPath, string args)
		{
			return Task.CompletedTask;
		}

		public Task ConfirmParametersAsync(ClassicTumblerParameters parameters, StandardCycle standardCyle)
		{
			return Task.CompletedTask;
		}
	}

	public class ClientInteractionException : Exception
	{
		public ClientInteractionException(string message) : base(message)
		{

		}
	}
}
