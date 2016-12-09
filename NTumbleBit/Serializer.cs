using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NTumbleBit.JsonConverters;
using NTumbleBit.PuzzleSolver;
using Newtonsoft.Json.Converters;
using NTumbleBit.ClassicTumbler;
using System;

namespace NTumbleBit
{
	public class Serializer
	{
		public static void RegisterFrontConverters(JsonSerializerSettings settings, Network network = null)
		{
			settings.Converters.Add(new RsaKeyJsonConverter());
			settings.Converters.Add(new SerializerBaseJsonConverter());
			settings.Converters.Add(new StringEnumConverter());
			NBitcoin.JsonConverters.Serializer.RegisterFrontConverters(settings, network);
		}

		public static T ToObject<T>(string data)
		{
			return ToObject<T>(data, null);
		}
		public static T ToObject<T>(string data, Network network)
		{
			JsonSerializerSettings settings = new JsonSerializerSettings
			{
				Formatting = Formatting.Indented
			};
			RegisterFrontConverters(settings, network);
			return JsonConvert.DeserializeObject<T>(data, settings);
		}

		public static string ToString<T>(T response, Network network)
		{
			JsonSerializerSettings settings = new JsonSerializerSettings
			{
				Formatting = Formatting.Indented
			};
			RegisterFrontConverters(settings, network);
			return JsonConvert.SerializeObject(response, settings);
		}
		public static string ToString<T>(T response)
		{
			return ToString<T>(response, null);
		}

		public static T Clone<T>(T data)
		{
			var o = ToString(data);
			return ToObject<T>(o);
		}
	}
}
