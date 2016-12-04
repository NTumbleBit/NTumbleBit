using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.JsonConverters
{
	public class SolverSerializerJsonConverter : JsonConverter
	{
		public SolverSerializerJsonConverter()
		{
			Support<ServerCommitment>((a, b) => a.WriteCommitment(b), a => a.ReadCommitment());
			Support<SolutionKey>((a, b) => a.WritePuzzleSolutionKey(b), a => a.ReadPuzzleSolutionKey());
			Support<BlindFactor>((a, b) => a.WriteBlindFactor(b), a => a.ReadBlindFactor());
		}

		Dictionary<Type, Tuple<Action<SolverSerializer, object>, Func<SolverSerializer, object>>> _Supports = new Dictionary<Type, Tuple<Action<SolverSerializer, object>, Func<SolverSerializer, object>>>();

		public void Support<T>(Action<SolverSerializer, T> serialize, Func<SolverSerializer, T> deserialize)
		{
			_Supports.Add(typeof(T), Tuple.Create<Action<SolverSerializer, object>, Func<SolverSerializer, object>>((a, b) => serialize(a, (T)b), a => deserialize(a)));
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
				var seria = CreateSolverSerializer(reader);
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

		private SolverSerializer CreateSolverSerializer(JsonReader reader)
		{
			return new SolverSerializer(new MemoryStream(Encoders.Hex.DecodeData((string)reader.Value)));
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if(value != null)
			{
				var write = _Supports[value.GetType()];
				var ms = new MemoryStream();
				var seria = new SolverSerializer(ms);
				write.Item1(seria, value);
				writer.WriteValue(Encoders.Hex.EncodeData(ms.ToArray()));
			}
		}
	}
}
