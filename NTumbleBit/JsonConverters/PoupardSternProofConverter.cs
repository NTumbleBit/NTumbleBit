using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NTumbleBit.BouncyCastle.Math;
using TumbleBitSetup;

namespace NTumbleBit.JsonConverters  
{
    /// <summary>
    /// Converter used to convert <see cref="PoupardSternProof"/> to and from JSON.
    /// </summary>
    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
    public class PoupardSternProofConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(PoupardSternProof);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject poupardSternProof = JObject.Load(reader);
            JToken xvalues = poupardSternProof["XValues"];
            List<BigInteger> xValuesList = new List<BigInteger>();

            foreach (var xvalue in xvalues)
            {
                byte[] bytesX = Convert.FromBase64String(xvalue.Value<string>());
                xValuesList.Add( new BigInteger(1, bytesX) );
            }

            JToken yvalue = poupardSternProof["YValue"];
            byte[] bytesY = Convert.FromBase64String(yvalue.Value<string>());
            BigInteger yValue = new BigInteger(1, bytesY);

            return new PoupardSternProof(Tuple.Create(xValuesList.ToArray(), yValue));
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("XValues");
            writer.WriteStartArray();
            var psp = value as PoupardSternProof;  
            foreach (var bint in psp.XValues)
            {
                byte[] bytesX = bint.ToByteArray();
                writer.WriteValue(Convert.ToBase64String(bytesX));
            }
            writer.WriteEndArray();
            writer.WritePropertyName("YValue");
            byte[] bytesY = psp.YValue.ToByteArray();
            writer.WriteValue(Convert.ToBase64String(bytesY));
            writer.WriteEndObject();
        }
    }
}
