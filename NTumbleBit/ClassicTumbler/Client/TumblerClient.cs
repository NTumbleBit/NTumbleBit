using NBitcoin;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NTumbleBit.ClassicTumbler.Server.Models;
using NTumbleBit.Logging;
using Microsoft.Extensions.Logging;
using System.IO;

namespace NTumbleBit.ClassicTumbler.Client
{
    public class TumblerClient
    {
		public TumblerClient(Network network, Uri serverAddress)
		{
			if(serverAddress == null)
				throw new ArgumentNullException(nameof(serverAddress));
			if(network == null)
				throw new ArgumentNullException(nameof(network));
			_Address = serverAddress;
			_Network = network;
		}


		private readonly Network _Network;
		public Network Network
		{
			get
			{
				return _Network;
			}
		}


		private readonly Uri _Address;
		public Uri Address
		{
			get
			{
				return _Address;
			}
		}

	    private static readonly HttpClient SharedClient = new HttpClient();
		internal HttpClient Client = SharedClient;

		public Task<ClassicTumblerParameters> GetTumblerParametersAsync(Identity who)
		{
			return GetAsync<ClassicTumblerParameters>(who, "api/v1/tumblers/0/parameters");
		}
		public ClassicTumblerParameters GetTumblerParameters(Identity who)
		{
			return GetTumblerParametersAsync(who).GetAwaiter().GetResult();
		}

	    private Task<T> GetAsync<T>(Identity who, string relativePath, params object[] parameters)
		{
			return SendAsync<T>(who, HttpMethod.Get, null, relativePath, parameters);
		}

		public UnsignedVoucherInformation AskUnsignedVoucher(Identity who)
		{
			return AskUnsignedVoucherAsync(who).GetAwaiter().GetResult();
		}

		public Task<UnsignedVoucherInformation> AskUnsignedVoucherAsync(Identity who)
		{
			return GetAsync<UnsignedVoucherInformation>(who, "api/v1/tumblers/0/vouchers/");
		}


		public Task<PuzzleSolution> SignVoucherAsync(Identity who, SignVoucherRequest signVoucherRequest)
		{
			return SendAsync<PuzzleSolution>(who, HttpMethod.Post, signVoucherRequest, "api/v1/tumblers/0/clientchannels/confirm");
		}
		public PuzzleSolution SignVoucher(Identity who, SignVoucherRequest signVoucherRequest)
		{
			return SignVoucherAsync(who, signVoucherRequest).GetAwaiter().GetResult();
		}

		public Task<ScriptCoin> OpenChannelAsync(Identity who, OpenChannelRequest request)
		{
			if(request == null)
				throw new ArgumentNullException(nameof(request));
			return SendAsync<ScriptCoin>(who, HttpMethod.Post, request, "api/v1/tumblers/0/channels/");
		}

		public ScriptCoin OpenChannel(Identity who, OpenChannelRequest request)
		{
			return OpenChannelAsync(who, request).GetAwaiter().GetResult();
		}

		public Task<TumblerEscrowKeyResponse> RequestTumblerEscrowKeyAsync(Identity who, int cycleStart)
		{
			return SendAsync<TumblerEscrowKeyResponse>(who, HttpMethod.Post, cycleStart, "api/v1/tumblers/0/clientchannels/");
		}
		public TumblerEscrowKeyResponse RequestTumblerEscrowKey(Identity who, int cycleStart)
		{
			return RequestTumblerEscrowKeyAsync(who, cycleStart).GetAwaiter().GetResult();
		}

		private string GetFullUri(string relativePath, params object[] parameters)
		{
			relativePath = String.Format(relativePath, parameters ?? new object[0]);
			var uri = Address.AbsoluteUri;
			if(!uri.EndsWith("/", StringComparison.Ordinal))
				uri += "/";
			uri += relativePath;
			return uri;
		}

		public static Identity CurrentIdentity { get; private set; } = Identity.DoesntMatter;
		private async Task<T> SendAsync<T>(Identity who, HttpMethod method, object body, string relativePath, params object[] parameters)
		{
			var uri = GetFullUri(relativePath, parameters);
			var message = new HttpRequestMessage(method, uri);
			if(body != null)
			{
				message.Content = new StringContent(Serializer.ToString(body, Network), Encoding.UTF8, "application/json");
			}

			if (Tor.UseTor)
			{
				if (who != CurrentIdentity)
				{
					var start = DateTime.Now;
					Logs.Client.LogInformation($"Changing identity to {who}");
					await Tor.ControlPortClient.ChangeCircuitAsync().ConfigureAwait(false);
					var takelong = start - DateTime.Now;
					File.AppendAllText("torchangelog.txt", Environment.NewLine + Environment.NewLine + $"CHANGE IP: {(int)takelong.TotalSeconds} sec" + Environment.NewLine);
				}
				CurrentIdentity = who;
			}

			File.AppendAllText("torchangelog.txt", '\t' + who.ToString() + Environment.NewLine);
			File.AppendAllText("torchangelog.txt", '\t' + message.Method.Method + " " + message.RequestUri.AbsolutePath + Environment.NewLine);
			HttpResponseMessage result;
			try
			{
				result = await Client.SendAsync(message).ConfigureAwait(false);
			}
			catch(Exception ex)
			{
				File.AppendAllText("torchangelog.txt", ex.ToString() + Environment.NewLine);
				throw;
			}
			if(result.StatusCode == HttpStatusCode.NotFound)
				return default(T);
			if(!result.IsSuccessStatusCode)
			{
				string error = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
				if(!string.IsNullOrEmpty(error))
				{
					throw new HttpRequestException(result.StatusCode + ": " + error);
				}
			}
			result.EnsureSuccessStatusCode();
			if(typeof(T) == typeof(byte[]))
				return (T)(object)await result.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
			var str = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
			if(typeof(T) == typeof(string))
				return (T)(object)str;
			return Serializer.ToObject<T>(str, Network);
		}

		public ServerCommitmentsProof CheckRevelation(Identity who, int cycleId, string channelId, PuzzlePromise.ClientRevelation revelation)
		{
			return CheckRevelationAsync(who, cycleId, channelId, revelation).GetAwaiter().GetResult();
		}

		private Task<ServerCommitmentsProof> CheckRevelationAsync(Identity who, int cycleId, string channelId, PuzzlePromise.ClientRevelation revelation)
		{
			return SendAsync<ServerCommitmentsProof>(who, HttpMethod.Post, revelation, "api/v1/tumblers/0/channels/{0}/{1}/checkrevelation", cycleId, channelId);
		}

		public Task<PuzzlePromise.ServerCommitment[]> SignHashesAsync(Identity who, int cycleId, string channelId, SignaturesRequest sigReq)
		{
			return SendAsync<PuzzlePromise.ServerCommitment[]>(who, HttpMethod.Post, sigReq, "api/v1/tumblers/0/channels/{0}/{1}/signhashes", cycleId, channelId);
		}

		public SolutionKey[] CheckRevelation(Identity who, int cycleId, string channelId, PuzzleSolver.ClientRevelation revelation)
		{
			return CheckRevelationAsync(who, cycleId, channelId, revelation).GetAwaiter().GetResult();
		}
		public Task<SolutionKey[]> CheckRevelationAsync(Identity who, int cycleId, string channelId, PuzzleSolver.ClientRevelation revelation)
		{
			return SendAsync<SolutionKey[]>(who, HttpMethod.Post, revelation, "api/v1/tumblers/0/clientschannels/{0}/{1}/checkrevelation", cycleId, channelId);
		}

		public OfferInformation CheckBlindFactors(Identity who, int cycleId, string channelId, BlindFactor[] blindFactors)
		{
			return CheckBlindFactorsAsync(who, cycleId, channelId, blindFactors).GetAwaiter().GetResult();
		}

		public Task<OfferInformation> CheckBlindFactorsAsync(Identity who, int cycleId, string channelId, BlindFactor[] blindFactors)
		{
			return SendAsync<OfferInformation>(who, HttpMethod.Post, blindFactors, "api/v1/tumblers/0/clientschannels/{0}/{1}/checkblindfactors", cycleId, channelId);
		}

		public PuzzleSolver.ServerCommitment[] SolvePuzzles(Identity who, int cycleId, string channelId, PuzzleValue[] puzzles)
		{
			return SolvePuzzlesAsync(who, cycleId, channelId, puzzles).GetAwaiter().GetResult();
		}

		public void SetHttpHandler(HttpMessageHandler handler)
		{
			Client = new HttpClient(handler);
		}

		public Task<PuzzleSolver.ServerCommitment[]> SolvePuzzlesAsync(Identity who, int cycleId, string channelId, PuzzleValue[] puzzles)
		{
			return SendAsync<PuzzleSolver.ServerCommitment[]>(who, HttpMethod.Post, puzzles, "api/v1/tumblers/0/clientchannels/{0}/{1}/solvepuzzles", cycleId, channelId);
		}



		public PuzzlePromise.ServerCommitment[] SignHashes(Identity who, int cycleId, string channelId, SignaturesRequest sigReq)
		{
			return SignHashesAsync(who, cycleId, channelId, sigReq).GetAwaiter().GetResult();
		}

		public SolutionKey[] FulfillOffer(Identity who, int cycleId, string channelId, TransactionSignature signature)
		{
			return FulfillOfferAsync(who, cycleId, channelId, signature).GetAwaiter().GetResult();
		}

		public Task<SolutionKey[]> FulfillOfferAsync(Identity who, int cycleId, string channelId, TransactionSignature signature)
		{
			return SendAsync<SolutionKey[]>(who, HttpMethod.Post, signature, "api/v1/tumblers/0/clientchannels/{0}/{1}/offer", cycleId, channelId);
		}

		public void GiveEscapeKey(Identity who, int cycleId, string channelId, TransactionSignature signature)
		{
			GiveEscapeKeyAsync(who, cycleId, channelId, signature).GetAwaiter().GetResult();
		}
		public Task GiveEscapeKeyAsync(Identity who, int cycleId, string channelId, TransactionSignature signature)
		{
			return SendAsync<string>(who, HttpMethod.Post, signature, "api/v1/tumblers/0/clientchannels/{0}/{1}/escape", cycleId, channelId);
		}
	}
}
