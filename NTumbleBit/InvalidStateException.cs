using System;
using System.Collections.Generic;
using System.Text;

namespace NTumbleBit
{
	public class InvalidStateException : InvalidOperationException
	{
		public InvalidStateException(string message) : base(message)
		{

		}
	}
}
