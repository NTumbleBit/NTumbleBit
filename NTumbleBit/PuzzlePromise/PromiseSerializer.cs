using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
	public class PromiseSerializer : SerializerBase
	{
		public PromiseSerializer(PromiseParameters parameters, Stream inner) : base(inner, parameters == null ? null : parameters.ServerKey._Key)
		{
			if(inner == null)
				throw new ArgumentNullException("inner");
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			_Parameters = parameters;
		}


		private readonly PromiseParameters _Parameters;
		public PromiseParameters Parameters
		{
			get
			{
				return _Parameters;
			}
		}
	}
}
