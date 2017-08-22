using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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
				context.Exception = null;
				context.ExceptionDispatchInfo = null;
				context.ExceptionHandled = true;
				context.Result = ((Controller)context.Controller).BadRequest("invalid-tumbler");
			}
			if(ex != null && ex.ParamName == "channelId")
			{
				context.Exception = null;
				context.ExceptionDispatchInfo = null;
				context.ExceptionHandled = true;
				context.Result = ((Controller)context.Controller).BadRequest("invalid-channel");
			}
			base.OnActionExecuted(context);
		}
	}
}
