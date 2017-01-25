using NBitcoin.DataEncoders;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.JsonConverters
{
	public class SerializerBaseJsonConverter : JsonConverter
	{
		public SerializerBaseJsonConverter()
		{
			Support<PuzzleValue>((a, b) => a.WritePuzzle(b), a => a.ReadPuzzle());
			Support<PuzzleSolution>((a, b) => a.WritePuzzleSolution(b), a => a.ReadPuzzleSolution());
			Support<SolutionKey>((a, b) => a.WritePuzzleSolutionKey(b), a => a.ReadPuzzleSolutionKey());
			Support<BlindFactor>((a, b) => a.WriteBlindFactor(b), a => a.ReadBlindFactor());
			Support<Quotient>((a, b) => a.WriteQuotient(b), a => a.ReadQuotient());
		}

		private Dictionary<Type, Tuple<Action<SerializerBase, object>, Func<SerializerBase, object>>> _Supports = new Dictionary<Type, Tuple<Action<SerializerBase, object>, Func<SerializerBase, object>>>();

		internal void Support<T>(Action<SerializerBase, T> serialize, Func<SerializerBase, T> deserialize)
		{
			_Supports.Add(typeof(T), Tuple.Create<Action<SerializerBase, object>, Func<SerializerBase, object>>((a, b) => serialize(a, (T)b), a => deserialize(a)));
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

		private SerializerBase CreateSolverSerializer(JsonReader reader)
		{
			return new SerializerBase(new MemoryStream(Encoders.Hex.DecodeData((string)reader.Value)));
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if(value != null)
			{
				var write = _Supports[value.GetType()];
				var ms = new MemoryStream();
				var seria = new SerializerBase(ms);
				write.Item1(seria, value);
				writer.WriteValue(Encoders.Hex.EncodeData(ms.ToArray()));
			}
		}
	}
}
