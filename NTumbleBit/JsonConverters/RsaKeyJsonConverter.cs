using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using NTumbleBit.JsonConverters;
using System;
using System.IO;
using System.Reflection;


namespace NTumbleBit.JsonConverters
{
	public class RsaKeyJsonConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return typeof(RsaPubKey).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo()) ||
				typeof(RsaKey).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if(reader.TokenType == JsonToken.Null)
				return null;

			try
			{
				if(objectType == typeof(RsaPubKey))
				{
					var bytes = Encoders.Hex.DecodeData((string)reader.Value);
					return new RsaPubKey(bytes);
				}
				else
				{
					var bytes = Encoders.Hex.DecodeData((string)reader.Value);
					return new RsaKey(bytes);
				}
			}
			catch(EndOfStreamException)
			{
			}
			catch(FormatException)
			{
			}
			throw new JsonObjectException("Invalid rsa object of type " + objectType.Name, reader);
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if(value != null)
			{
				if(value is RsaPubKey)
				{
					var bytes = ((RsaPubKey)value).ToBytes();
					writer.WriteValue(Encoders.Hex.EncodeData(bytes));
				}
				else
				{
					var bytes = ((RsaKey)value).ToBytes();
					writer.WriteValue(Encoders.Hex.EncodeData(bytes));
				}
			}
		}
	}
}