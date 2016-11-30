using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;


#if !CLIENT
namespace NTumbleBit.TumblerServer.JsonConverters
#else
namespace NTumbleBit.Client.Tumbler.JsonConverters
#endif
{
	class RsaPubKeyJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(RsaPubKey).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            try
            {

                var bytes = Encoders.Hex.DecodeData((string)reader.Value);
				return new RsaPubKey(bytes);
            }
            catch (EndOfStreamException)
            {
            }
            catch (FormatException)
            {
            }
            throw new JsonObjectException("Invalid bitcoin object of type " + objectType.Name, reader);
        }
		
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var bytes = ((RsaPubKey)value).ToBytes();
            writer.WriteValue(Encoders.Hex.EncodeData(bytes));
        }
    }
}