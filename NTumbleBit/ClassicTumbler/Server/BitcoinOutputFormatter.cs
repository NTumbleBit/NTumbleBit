using Microsoft.AspNetCore.Mvc.Formatters;
using System.Reflection;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler.Server
{
	public class BitcoinOutputFormatter : IOutputFormatter
	{
		public bool CanWriteResult(OutputFormatterCanWriteContext context)
		{
			return (typeof(IBitcoinSerializable).GetTypeInfo().IsAssignableFrom(context.ObjectType)) ||
				(context.ObjectType.IsArray && typeof(IBitcoinSerializable).GetTypeInfo().IsAssignableFrom(context.ObjectType.GetTypeInfo().GetElementType()));
		}

		public Task WriteAsync(OutputFormatterWriteContext context)
		{
			var obj = context.Object;
			if(context.ObjectType.IsArray)
			{
				var arrayWrapper = typeof(ArrayWrapper<>).GetTypeInfo().MakeGenericType(context.ObjectType.GetElementType());
				obj = Activator.CreateInstance(arrayWrapper, context.Object);
			}
			var bytes = ((IBitcoinSerializable)obj).ToBytes();
			context.HttpContext.Response.StatusCode = 200;
			return context.HttpContext.Response.Body.WriteAsync(bytes, 0, bytes.Length);
		}
	}
}
