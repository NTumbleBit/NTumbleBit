using Microsoft.AspNetCore.Mvc.ModelBinding;
using NBitcoin;
using System.Reflection;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Internal;
using NTumbleBit.ClassicTumbler.Server;

namespace NTumbleBit.ClassicTumbler
{
	public class TumblerParametersModelBinder : IModelBinder
	{
		public TumblerParametersModelBinder()
		{

		}

		#region IModelBinder Members

		public Task BindModelAsync(ModelBindingContext bindingContext)
		{
			if(!(typeof(ClassicTumblerParameters).GetTypeInfo().IsAssignableFrom(bindingContext.ModelType)))
			{
				return TaskCache.CompletedTask;
			}

			ValueProviderResult val = bindingContext.ValueProvider.GetValue(
				bindingContext.ModelName);
			if(val == null)
			{
				return TaskCache.CompletedTask;
			}

			string key = val.FirstValue as string;
			if(key == null)
			{
				return TaskCache.CompletedTask;
			}

			try
			{
				var id = new uint160(key);
				var runtime = (TumblerRuntime)bindingContext.HttpContext.RequestServices.GetService(typeof(TumblerRuntime));
				if(runtime.ClassicTumblerParametersHash != id)
				{
					bindingContext.Result = ModelBindingResult.Failed();
				}
				else
					bindingContext.Result = ModelBindingResult.Success(runtime.ClassicTumblerParameters);
			}
			catch(FormatException)
			{
				bindingContext.Result = ModelBindingResult.Failed();
			}
			return TaskCache.CompletedTask;
		}

		#endregion
	}
}
