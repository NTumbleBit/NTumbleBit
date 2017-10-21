using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler.Server
{
    public class ActionResultExceptionFilter : ActionFilterAttribute
	{
		public override void OnActionExecuted(ActionExecutedContext context)
		{
            if (context.Exception is ActionResultException ex)
            {
                context.Exception = null;
                context.ExceptionDispatchInfo = null;
                context.ExceptionHandled = true;
                context.Result = ex.Result;
            }
            base.OnActionExecuted(context);
		}
	}
}
