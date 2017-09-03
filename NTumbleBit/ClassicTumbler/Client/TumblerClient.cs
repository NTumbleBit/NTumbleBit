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
using System.IO;
using System.Threading;

namespace NTumbleBit.ClassicTumbler.Client
{
	public class TumblerClient : IDisposable
	{
		public TumblerClient(Network network, TumblerUrlBuilder serverAddress, int cycleId)
		{
			if(serverAddress == null)
				throw new ArgumentNullException(nameof(serverAddress));
			if(network == null)
				throw new ArgumentNullException(nameof(network));
			_Address = serverAddress;
			_Network = network;
			this.cycleId = cycleId;
		}

		private int cycleId;

		private readonly Network _Network;
		public Network Network
		{
			get
			{
				return _Network;
			}
		}


		private readonly TumblerUrlBuilder _Address;

		private static readonly HttpClient SharedClient = new HttpClient(Utils.SetAntiFingerprint(new HttpClientHandler()));

		internal HttpClient Client = SharedClient;

		public Task<ClassicTumblerParameters> GetTumblerParametersAsync()
		{
			return GetAsync<ClassicTumblerParameters>($"parameters");
		}
		public ClassicTumblerParameters GetTumblerParameters()
		{
			return GetTumblerParametersAsync().GetAwaiter().GetResult();
		}

		private Task<T> GetAsync<T>(string relativePath, params object[] parameters) where T : IBitcoinSerializable, new()
		{
			return SendAsync<T>(HttpMethod.Get, null, relativePath, parameters);
		}

		public UnsignedVoucherInformation AskUnsignedVoucher()
		{
			return AskUnsignedVoucherAsync().GetAwaiter().GetResult();
		}

		public Task<UnsignedVoucherInformation> AskUnsignedVoucherAsync()
		{
			return GetAsync<UnsignedVoucherInformation>($"vouchers/");
		}


		public Task<PuzzleSolution> SignVoucherAsync(SignVoucherRequest signVoucherRequest)
		{
			return SendAsync<PuzzleSolution>(HttpMethod.Post, signVoucherRequest, $"clientchannels/confirm");
		}
		public PuzzleSolution SignVoucher(SignVoucherRequest signVoucherRequest)
		{
			return SignVoucherAsync(signVoucherRequest).GetAwaiter().GetResult();
		}

		public async Task<uint160> BeginOpenChannelAsync(OpenChannelRequest request)
		{
			if(request == null)
				throw new ArgumentNullException(nameof(request));
			var c = await SendAsync<uint160.MutableUint160>(HttpMethod.Post, request, $"channels/beginopen").ConfigureAwait(false);
			return c?.Value;
		}

		public TumblerEscrowData EndOpenChannel(int cycleId, uint160 channelId)
		{
			return EndOpenChannelAsync(cycleId, channelId).GetAwaiter().GetResult();
		}

		public async Task<TumblerEscrowData> EndOpenChannelAsync(int cycleId, uint160 channelId)
		{
			if(channelId == null)
				throw new ArgumentNullException(nameof(channelId));
			var c = await SendAsync<TumblerEscrowData>(HttpMethod.Post, null, $"channels/{cycleId}/{channelId}/endopen").ConfigureAwait(false);
			return c;
		}

		public uint160 BeginOpenChannel(OpenChannelRequest request)
		{
			return BeginOpenChannelAsync(request).GetAwaiter().GetResult();
		}

		public Task<TumblerEscrowKeyResponse> RequestTumblerEscrowKeyAsync()
		{
			return SendAsync<TumblerEscrowKeyResponse>(HttpMethod.Get, null, $"clientchannels/{cycleId}/");
		}
		public TumblerEscrowKeyResponse RequestTumblerEscrowKey()
		{
			return RequestTumblerEscrowKeyAsync().GetAwaiter().GetResult();
		}

		private string GetFullUri(string relativePath, params object[] parameters)
		{
			relativePath = String.Format(relativePath, parameters ?? new object[0]);

			var uri = _Address.GetRoutableUri(true).AbsoluteUri;
			if(!uri.EndsWith("/", StringComparison.Ordinal))
				uri += "/";
			uri += relativePath;
			return uri;
		}

		int MaxContentLength = 1024 * 1024;

		public TimeSpan RequestTimeout
		{
			get; set;
		} = TimeSpan.FromMinutes(1.0);

		private async Task<T> SendAsync<T>(HttpMethod method, IBitcoinSerializable body, string relativePath, params object[] parameters) where T : IBitcoinSerializable, new()
		{
			var uri = GetFullUri(relativePath, parameters);
			var message = new HttpRequestMessage(method, uri);
			if(body != null)
			{
				message.Content = new ByteArrayContent(body.ToBytes());
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
			if(result.Content?.Headers?.ContentLength > MaxContentLength)
				throw new IOException("Content is too big");

			result.EnsureSuccessStatusCode();
			if(typeof(T) == typeof(byte[]))
				return (T)(object)await result.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
			var str = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
			if(typeof(T) == typeof(string))
				return (T)(object)str;

			var bytes = await result.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
			if(bytes.Length == 0)
				return default(T);
			var stream = new BitcoinStream(new MemoryStream(bytes), false);

			var data = new T();
			stream.ReadWrite<T>(ref data);
			return data;
		}

		public ServerCommitmentsProof CheckRevelation(uint160 channelId, PuzzlePromise.ClientRevelation revelation)
		{
			return CheckRevelationAsync(channelId, revelation).GetAwaiter().GetResult();
		}

		private Task<ServerCommitmentsProof> CheckRevelationAsync(uint160 channelId, PuzzlePromise.ClientRevelation revelation)
		{
			return SendAsync<ServerCommitmentsProof>(HttpMethod.Post, revelation, $"channels/{cycleId}/{channelId}/checkrevelation");
		}

		public async Task<PuzzlePromise.ServerCommitment[]> SignHashesAsync(uint160 channelId, SignaturesRequest sigReq)
		{
			var result = await SendAsync<ArrayWrapper<PuzzlePromise.ServerCommitment>>(HttpMethod.Post, sigReq, $"channels/{cycleId}/{channelId}/signhashes").ConfigureAwait(false);
			return result.Elements;
		}

		public SolutionKey[] CheckRevelation(uint160 channelId, PuzzleSolver.ClientRevelation revelation)
		{
			return CheckRevelationAsync(channelId, revelation).GetAwaiter().GetResult();
		}
		public async Task<SolutionKey[]> CheckRevelationAsync(uint160 channelId, PuzzleSolver.ClientRevelation revelation)
		{
			var result = await SendAsync<ArrayWrapper<SolutionKey>>(HttpMethod.Post, revelation, $"clientschannels/{cycleId}/{channelId}/checkrevelation").ConfigureAwait(false);
			return result.Elements;
		}

		public OfferInformation CheckBlindFactors(uint160 channelId, BlindFactor[] blindFactors)
		{
			return CheckBlindFactorsAsync(channelId, blindFactors).GetAwaiter().GetResult();
		}

		public Task<OfferInformation> CheckBlindFactorsAsync(uint160 channelId, BlindFactor[] blindFactors)
		{
			return SendAsync<OfferInformation>(HttpMethod.Post, new ArrayWrapper<BlindFactor>(blindFactors), $"clientschannels/{cycleId}/{channelId}/checkblindfactors");
		}

		public PuzzleSolver.ServerCommitment[] SolvePuzzles(uint160 channelId, PuzzleValue[] puzzles)
		{
			return SolvePuzzlesAsync(channelId, puzzles).GetAwaiter().GetResult();
		}

		public void SetHttpHandler(HttpMessageHandler handler)
		{
			Client = new HttpClient(handler);
		}

		public async Task<PuzzleSolver.ServerCommitment[]> SolvePuzzlesAsync(uint160 channelId, PuzzleValue[] puzzles)
		{
			var result = await SendAsync<ArrayWrapper<PuzzleSolver.ServerCommitment>>(HttpMethod.Post, new ArrayWrapper<PuzzleValue>(puzzles), $"clientchannels/{cycleId}/{channelId}/solvepuzzles").ConfigureAwait(false);
			return result.Elements;
		}



		public PuzzlePromise.ServerCommitment[] SignHashes(uint160 channelId, SignaturesRequest sigReq)
		{
			return SignHashesAsync(channelId, sigReq).GetAwaiter().GetResult();
		}

		public SolutionKey[] FulfillOffer(uint160 channelId, TransactionSignature signature)
		{
			return FulfillOfferAsync(cycleId, channelId, signature).GetAwaiter().GetResult();
		}

		public async Task<SolutionKey[]> FulfillOfferAsync(int cycleId, uint160 channelId, TransactionSignature signature)
		{
			var result = await SendAsync<ArrayWrapper<SolutionKey>>(HttpMethod.Post, new SignatureWrapper(signature), $"clientchannels/{cycleId}/{channelId}/offer").ConfigureAwait(false);
			return result.Elements;
		}

		public void GiveEscapeKey(uint160 channelId, TransactionSignature signature)
		{
			GiveEscapeKeyAsync(channelId, signature).GetAwaiter().GetResult();
		}
		public Task GiveEscapeKeyAsync(uint160 channelId, TransactionSignature signature)
		{
			return SendAsync<NoData>(HttpMethod.Post, new SignatureWrapper(signature), $"clientchannels/{cycleId}/{channelId}/escape");
		}

		public void Dispose()
		{
			if(Client != SharedClient)
				Client.Dispose();
		}
	}
}
