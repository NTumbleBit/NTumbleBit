using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using NTumbleBit.PuzzlePromise;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.JsonConverters
{
	public class PromiseSerializerJsonConverter : JsonConverter
	{
		public PromiseSerializerJsonConverter()
		{
			Support<Quotient>((a, b) => a.WriteQuotient(b), a => a.ReadQuotient());
		}

		Dictionary<Type, Tuple<Action<PromiseSerializer, object>, Func<PromiseSerializer, object>>> _Supports = new Dictionary<Type, Tuple<Action<PromiseSerializer, object>, Func<PromiseSerializer, object>>>();

		public void Support<T>(Action<PromiseSerializer, T> serialize, Func<PromiseSerializer, T> deserialize)
		{
			_Supports.Add(typeof(T), Tuple.Create<Action<PromiseSerializer, object>, Func<PromiseSerializer, object>>((a, b) => serialize(a, (T)b), a => deserialize(a)));
		}		


		public override bool CanConvert(Type objectType)
		{
			return _Supports.ContainsKey(objectType);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if(reader.TokenType == JsonToken.Null)
				return null;

			try
			{
				var read = _Supports[objectType];
				var seria = CreatePromiseSerializer(reader);
				return read.Item2(seria);
			}
			catch(EndOfStreamException)
			{
			}
			catch(FormatException)
			{
			}

			throw new JsonObjectException("Invalid rsa object of type " + objectType.Name, reader);
		}

		private PromiseSerializer CreatePromiseSerializer(JsonReader reader)
		{
			return new PromiseSerializer(new MemoryStream(Encoders.Hex.DecodeData((string)reader.Value)));
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if(value != null)
			{
				var write = _Supports[value.GetType()];
				var ms = new MemoryStream();
				var seria = new PromiseSerializer(ms);
				write.Item1(seria, value);
				writer.WriteValue(Encoders.Hex.EncodeData(ms.ToArray()));
			}
		}
	}
}
