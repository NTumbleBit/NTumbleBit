using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler.Server
{
	public class BitcoinSerializableResult : IActionResult
	{
		public BitcoinSerializableResult(IBitcoinSerializable data)
		{
			_Data = data;
		}
		IBitcoinSerializable _Data;

		public Task ExecuteResultAsync(ActionContext context)
		{
			var bytes = _Data.ToBytes();
			context.HttpContext.Response.StatusCode = 200;
			context.HttpContext.Response.Body.Write(bytes, 0, bytes.Length);
			return Task.CompletedTask;
		}
	}
}
