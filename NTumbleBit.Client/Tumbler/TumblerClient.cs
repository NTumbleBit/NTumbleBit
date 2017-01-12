using NBitcoin;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.Client.Tumbler.Models;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.Client.Tumbler
{
    public class TumblerClient
    {
		public TumblerClient(Network network, Uri serverAddress)
		{
			if(serverAddress == null)
				throw new ArgumentNullException("serverAddress");
			if(network == null)
				throw new ArgumentNullException("network");
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

		readonly static HttpClient Client = new HttpClient();
		public Task<ClassicTumblerParameters> GetTumblerParametersAsync()
		{
			return GetAsync<ClassicTumblerParameters>("api/v1/tumblers/0/parameters");
		}
		public ClassicTumblerParameters GetTumblerParameters()
		{
			return GetTumblerParametersAsync().GetAwaiter().GetResult();
		}

		Task<T> GetAsync<T>(string relativePath, params object[] parameters)
		{
			return SendAsync<T>(HttpMethod.Get, null, relativePath, parameters);
		}

		public UnsignedVoucherInformation AskUnsignedVoucher()
		{
			return AskUnsignedVoucherAsync().GetAwaiter().GetResult();
		}

		public Task<UnsignedVoucherInformation> AskUnsignedVoucherAsync()
		{
			return GetAsync<UnsignedVoucherInformation>("api/v1/tumblers/0/vouchers/");
		}


		public Task<PuzzleSolution> SignVoucherAsync(SignVoucherRequest signVoucherRequest)
		{
			return SendAsync<PuzzleSolution>(HttpMethod.Post, signVoucherRequest, "api/v1/tumblers/0/clientchannels/confirm");
		}
		public PuzzleSolution SignVoucher(SignVoucherRequest signVoucherRequest)
		{
			return SignVoucherAsync(signVoucherRequest).GetAwaiter().GetResult();
		}

		public Task<ScriptCoin> OpenChannelAsync(OpenChannelRequest request)
		{
			if(request == null)
				throw new ArgumentNullException("request");
			return SendAsync<ScriptCoin>(HttpMethod.Post, request, "api/v1/tumblers/0/channels/");
		}

		public ScriptCoin OpenChannel(OpenChannelRequest request)
		{
			return OpenChannelAsync(request).GetAwaiter().GetResult();
		}

		public Task<TumblerEscrowKeyResponse> RequestTumblerEscrowKeyAsync(int cycleStart)
		{
			return SendAsync<TumblerEscrowKeyResponse>(HttpMethod.Post, cycleStart, "api/v1/tumblers/0/clientchannels/");
		}
		public TumblerEscrowKeyResponse RequestTumblerEscrowKey(int cycleStart)
		{
			return RequestTumblerEscrowKeyAsync(cycleStart).GetAwaiter().GetResult();
		}

		private string GetFullUri(string relativePath, params object[] parameters)
		{
			relativePath = String.Format(relativePath, parameters ?? new object[0]);
			var uri = Address.AbsoluteUri;
			if(!uri.EndsWith("/"))
				uri += "/";
			uri += relativePath;
			return uri;
		}
		
		async Task<T> SendAsync<T>(HttpMethod method, object body, string relativePath, params object[] parameters)
		{
			var uri = GetFullUri(relativePath, parameters);
			var message = new HttpRequestMessage(method, uri);
			if(body != null)
			{
				message.Content = new StringContent(Serializer.ToString(body, Network), Encoding.UTF8, "application/json");
			}
			var result = await Client.SendAsync(message).ConfigureAwait(false);
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

		public ServerCommitmentsProof CheckRevelation(int cycleId, string channelId, PuzzlePromise.ClientRevelation revelation)
		{
			return CheckRevelationAsync(cycleId, channelId, revelation).GetAwaiter().GetResult();
		}

		private Task<ServerCommitmentsProof> CheckRevelationAsync(int cycleId, string channelId, PuzzlePromise.ClientRevelation revelation)
		{
			return SendAsync<ServerCommitmentsProof>(HttpMethod.Post, revelation, "api/v1/tumblers/0/channels/{0}/{1}/checkrevelation", cycleId, channelId);
		}

		public Task<PuzzlePromise.ServerCommitment[]> SignHashesAsync(int cycleId, string channelId, SignaturesRequest sigReq)
		{
			return SendAsync<PuzzlePromise.ServerCommitment[]>(HttpMethod.Post, sigReq, "api/v1/tumblers/0/channels/{0}/{1}/signhashes", cycleId, channelId);
		}		

		public SolutionKey[] CheckRevelation(int cycleId, string channelId, PuzzleSolver.ClientRevelation revelation)
		{
			return CheckRevelationAsync(cycleId, channelId, revelation).GetAwaiter().GetResult();
		}
		public Task<SolutionKey[]> CheckRevelationAsync(int cycleId, string channelId, PuzzleSolver.ClientRevelation revelation)
		{
			return SendAsync<SolutionKey[]>(HttpMethod.Post, revelation, "api/v1/tumblers/0/clientschannels/{0}/{1}/checkrevelation", cycleId, channelId);
		}

		public OfferInformation CheckBlindFactors(int cycleId, string channelId, BlindFactor[] blindFactors)
		{
			return CheckBlindFactorsAsync(cycleId, channelId, blindFactors).GetAwaiter().GetResult();
		}

		public Task<OfferInformation> CheckBlindFactorsAsync(int cycleId, string channelId, BlindFactor[] blindFactors)
		{
			return SendAsync<OfferInformation>(HttpMethod.Post, blindFactors, "api/v1/tumblers/0/clientschannels/{0}/{1}/checkblindfactors", cycleId, channelId);
		}

		public PuzzleSolver.ServerCommitment[] SolvePuzzles(int cycleId, string channelId, PuzzleValue[] puzzles)
		{
			return SolvePuzzlesAsync(cycleId, channelId, puzzles).GetAwaiter().GetResult();
		}

		public Task<PuzzleSolver.ServerCommitment[]> SolvePuzzlesAsync(int cycleId, string channelId, PuzzleValue[] puzzles)
		{
			return SendAsync<PuzzleSolver.ServerCommitment[]>(HttpMethod.Post, puzzles, "api/v1/tumblers/0/clientchannels/{0}/{1}/solvepuzzles", cycleId, channelId);
		}



		public PuzzlePromise.ServerCommitment[] SignHashes(int cycleId, string channelId, SignaturesRequest sigReq)
		{
			return SignHashesAsync(cycleId, channelId, sigReq).GetAwaiter().GetResult();
		}

		public SolutionKey[] FullfillOffer(int cycleId, string channelId, TransactionSignature clientSignature)
		{
			return FullfillOfferAsync(cycleId, channelId, clientSignature).GetAwaiter().GetResult();
		}

		public Task<SolutionKey[]> FullfillOfferAsync(int cycleId, string channelId, TransactionSignature clientSignature)
		{
			return SendAsync<SolutionKey[]>(HttpMethod.Post, clientSignature, "api/v1/tumblers/0/clientchannels/{0}/{1}/offer", cycleId, channelId);
		}
	}
}
