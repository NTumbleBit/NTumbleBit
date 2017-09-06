using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Filters;
using NTumbleBit.Logging;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Text;

namespace NTumbleBit.ClassicTumbler.Server
{
	public class TumblerExceptionFilter : ActionFilterAttribute
	{
		public override void OnActionExecuted(ActionExecutedContext context)
		{
			var ex = context.Exception as ArgumentNullException;
			if(ex != null && ex.ParamName == "tumblerId")
			{
				Log(ex);
				context.Exception = null;
				context.ExceptionDispatchInfo = null;
				context.ExceptionHandled = true;
				context.Result = ((Controller)context.Controller).BadRequest("invalid-tumbler");
			}
			if(ex != null && ex.ParamName == "channelId")
			{
				Log(ex);
				context.Exception = null;
				context.ExceptionDispatchInfo = null;
				context.ExceptionHandled = true;
				context.Result = ((Controller)context.Controller).BadRequest("invalid-channel");
			}
			var invalidState = context.Exception as InvalidStateException;
			if(invalidState != null)
			{
				Log(invalidState);
				context.Exception = null;
				context.ExceptionDispatchInfo = null;
				context.ExceptionHandled = true;
				context.Result = ((Controller)context.Controller).BadRequest("invalid-state");
			}

			var puzzleException = context.Exception as PuzzleException;
			if(puzzleException != null)
			{
				Log(puzzleException);
				context.Exception = null;
				context.ExceptionDispatchInfo = null;
				context.ExceptionHandled = true;
				context.Result = ((Controller)context.Controller).BadRequest("protocol-failure");
			}
			base.OnActionExecuted(context);
		}

		private void Log(Exception ex)
		{
			Logs.Tumbler.LogDebug(new EventId(), ex, "TumblerExceptionFilter handled an exception");
		}
	}
}
