using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NTumbleBit.JsonConverters
{
    public class OverridesJsonConverter : JsonConverter
	{
		public OverridesJsonConverter(JsonConverter inner)
		{
			_Inner = inner;
		}

		JsonConverter _Inner;
		public HashSet<Type> MaskTypes
		{
			get; set;
		} = new HashSet<Type>();

		public override bool CanConvert(Type objectType)
		{
			if(MaskTypes.Contains(objectType))
				return false;
			return _Inner.CanConvert(objectType);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			return _Inner.ReadJson(reader, objectType, existingValue, serializer);
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			_Inner.WriteJson(writer, value, serializer);
		}
	}
}
