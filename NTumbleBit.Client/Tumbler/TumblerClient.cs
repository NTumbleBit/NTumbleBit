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

		public PuzzleValue AskUnsignedVoucher()
		{
			return AskUnsignedVoucherAsync().GetAwaiter().GetResult();
		}

		public Task<PuzzleValue> AskUnsignedVoucherAsync()
		{
			return GetAsync<PuzzleValue>("api/v1/tumblers/0/askvoucher/");
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
				//string error = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
				//if(!string.IsNullOrEmpty(error))
				//{
				//	try
				//	{
				//		var errorObject = Serializer.ToObject<QBitNinjaError>(error, Network);
				//		if(errorObject.StatusCode != 0)
				//			throw new QBitNinjaException(errorObject);
				//	}
				//	catch(JsonSerializationException)
				//	{
				//	}
				//	catch(JsonReaderException)
				//	{
				//	}
				//}
			}
			result.EnsureSuccessStatusCode();
			if(typeof(T) == typeof(byte[]))
				return (T)(object)await result.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
			var str = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
			if(typeof(T) == typeof(string))
				return (T)(object)str;
			return Serializer.ToObject<T>(str, Network);
		}
	}
}
