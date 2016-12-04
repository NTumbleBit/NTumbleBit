using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.JsonConverters
{
	public class ECDSASignatureJsonConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(ECDSASignature);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			try
			{
				return reader.TokenType == JsonToken.Null ? null : new ECDSASignature(Encoders.Hex.DecodeData((string)reader.Value));
			}
			catch
			{
				throw new JsonObjectException("Invalid hex", reader);
			}
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if(value != null)
			{
				writer.WriteValue(Encoders.Hex.EncodeData(((ECDSASignature)value).ToDER()));
			}
		}
	}
}
