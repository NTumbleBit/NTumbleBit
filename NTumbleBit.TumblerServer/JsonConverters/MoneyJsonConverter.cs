using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Reflection;

#if !CLIENT
namespace NTumbleBit.TumblerServer.JsonConverters
#else
namespace NTumbleBit.Client.Tumbler.JsonConverters
#endif
{
#if !NOJSONNET
	public
#else
	internal
#endif
	class MoneyJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(Money).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                return reader.TokenType == JsonToken.Null ? null : new Money((long)reader.Value);
            }
            catch (InvalidCastException)
            {
                throw new JsonObjectException("Money amount should be in satoshi", reader);
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(((Money)value).Satoshi);
        }
    }
}

