using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler.CLI
{
	public interface ClientInteraction
	{
		Task ConfirmParametersAsync(ClassicTumblerParameters parameters);
	}

	public class AcceptAllClientInteraction : ClientInteraction
	{
		public Task ConfirmParametersAsync(ClassicTumblerParameters parameters)
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
