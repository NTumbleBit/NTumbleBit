using Microsoft.AspNetCore.Mvc.Formatters;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace NTumbleBit.ClassicTumbler.Server
{
	public class BitcoinInputFormatter : IInputFormatter
	{
		static BitcoinInputFormatter()
		{
			_Parse = typeof(BitcoinInputFormatter).GetTypeInfo().GetMethod("Parse", BindingFlags.Static | BindingFlags.Public);
		}

		static readonly MethodInfo _Parse;

		public bool CanRead(InputFormatterContext context)
		{
			return (typeof(IBitcoinSerializable).GetTypeInfo().IsAssignableFrom(context.ModelType)) ||
				(context.ModelType.IsArray && typeof(IBitcoinSerializable).GetTypeInfo().IsAssignableFrom(context.ModelType.GetTypeInfo().GetElementType()));
		}

		public Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
		{
			try
			{

				BitcoinStream bs = new BitcoinStream(context.HttpContext.Request.Body, false);
				Type type = context.ModelType;

				var signature = type == typeof(TransactionSignature);
				if(context.ModelType.IsArray)
				{
					var elementType = context.ModelType.GetElementType();
					type = typeof(ArrayWrapper<>).MakeGenericType(elementType);
				}
				if(signature)
				{
					type = typeof(SignatureWrapper);
				}

				var result = _Parse.MakeGenericMethod(type).Invoke(null, new object[] { bs });

				if(context.ModelType.IsArray)
				{
					var getElements = type.GetTypeInfo().GetProperty("Elements", BindingFlags.Instance | BindingFlags.Public).GetGetMethod();
					result = getElements.Invoke(result, new object[0]);
				}
				if(signature)
				{
					result = ((SignatureWrapper)result).Signature;
				}
				return InputFormatterResult.SuccessAsync(result);
			}
			catch
			{
				return InputFormatterResult.FailureAsync();
			}
		}

		public static T Parse<T>(BitcoinStream bs) where T : IBitcoinSerializable
		{
			T t = default(T);
			bs.ReadWrite(ref t);
			return t;
		}
	}
}
