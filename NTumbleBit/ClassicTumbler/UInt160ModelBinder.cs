using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Reflection;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Internal;

namespace NTumbleBit.ClassicTumbler
{
	public class UInt160ModelBinder : IModelBinder
	{
		#region IModelBinder Members

		public Task BindModelAsync(ModelBindingContext bindingContext)
		{
			if(!typeof(uint160).GetTypeInfo().IsAssignableFrom(bindingContext.ModelType))
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
				bindingContext.Model = null;
				return TaskCache.CompletedTask;
			}
			try
			{

				var value = uint160.Parse(key);
				if(value.ToString().StartsWith(uint160.Zero.ToString()))
					throw new FormatException("Invalid hash format");
				bindingContext.Result = ModelBindingResult.Success(value);
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
