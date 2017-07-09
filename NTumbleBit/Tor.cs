using DotNetTor.SocksPort;
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NTumbleBit.Logging;
using Microsoft.Extensions.Logging;

namespace NTumbleBit
{
	public static class Tor
	{
		public static Process TorProcess = null;
		public static bool UseTor = false;
		public const int SOCKSPort = 37124;
		public const int ControlPort = 37125;
		public const string HashedControlPassword = "16:0978DBAF70EEB5C46063F3F6FD8CBC7A86DF70D2206916C1E2AE29EAF6";
		public const string DataDirectory = "TorData";
		public static string TorArguments => $"SOCKSPort {SOCKSPort} ControlPort {ControlPort} HashedControlPassword {HashedControlPassword} DataDirectory {DataDirectory}";
		public static SocksPortHandler SocksPortHandler = new SocksPortHandler("127.0.0.1", socksPort: SOCKSPort);
		public static DotNetTor.ControlPort.Client ControlPortClient = new DotNetTor.ControlPort.Client("127.0.0.1", controlPort: ControlPort, password: "ILoveBitcoin21");
		public static TorState State = TorState.NotStarted;
		public static CancellationTokenSource CircuitEstabilishingJobCancel = new CancellationTokenSource();

		public static async Task MakeSureCircuitEstabilishedAsync()
		{
			var ctsToken = CircuitEstabilishingJobCancel.Token;
			State = TorState.NotStarted;
			while (true)
			{
				try
				{
					if (ctsToken.IsCancellationRequested) return;

					try
					{
						var estabilished = await ControlPortClient.IsCircuitEstabilishedAsync().WithTimeout(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
						if (ctsToken.IsCancellationRequested) return;

						if (estabilished)
						{
							State = TorState.CircuitEstabilished;
						}
						else
						{
							State = TorState.EstabilishingCircuit;
						}
					}
					catch (Exception ex)
					{
						if (ex is TimeoutException || ex.InnerException is TimeoutException)
						{
							// Tor messed up something internally, this sometimes happens when it creates new datadir (at first launch)
							// Restarting solves the issue
							await RestartAsync().ConfigureAwait(false);
						}
						if (ctsToken.IsCancellationRequested) return;
					}
				}
				catch(Exception ex)
				{
					Logs.Client.LogInformation("Ignoring Tor exception");
					Logs.Client.LogInformation(ex.ToString());
				}

				if (State == TorState.CircuitEstabilished)
				{
					return;
				}
				var wait = TimeSpan.FromSeconds(3);
				await Task.Delay(wait, ctsToken).ContinueWith(tsk => { }).ConfigureAwait(false);
			}
		}

		public static async Task RestartAsync()
		{
			Kill();

			await Task.Delay(3000).ContinueWith(tsk => { }).ConfigureAwait(false);

			try
			{
				Logs.Client.LogInformation("Starting Tor process.");
				TorProcess.Start();

				CircuitEstabilishingJobCancel = new CancellationTokenSource();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
				MakeSureCircuitEstabilishedAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			}
			catch (Exception ex)
			{
				Logs.Client.LogInformation("Ignoring Tor exception:");
				Logs.Client.LogInformation(ex.ToString());
				State = TorState.NotStarted;
			}
		}

		public static void Kill()
		{
			CircuitEstabilishingJobCancel.Cancel();
			State = TorState.NotStarted;
			if (TorProcess != null && !TorProcess.HasExited)
			{
				Logs.Client.LogInformation("Terminating Tor process.");
				TorProcess.Kill();
			}
		}

		public enum TorState
		{
			NotStarted,
			EstabilishingCircuit,
			CircuitEstabilished
		}
	}
}
