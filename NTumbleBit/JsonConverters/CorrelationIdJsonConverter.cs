using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NTumbleBit.JsonConverters
{
	class CorrelationIdJsonConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return typeof(CorrelationId) == objectType;
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if(reader.TokenType == JsonToken.Null)
				return null;
			try
			{
				return CorrelationId.Parse((string)reader.Value);
			}
			catch(EndOfStreamException)
			{
			}
			catch(FormatException)
			{
			}
			throw new JsonObjectException("Invalid CorrelationId", reader);
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(value.ToString());
		}
	}
}
