using System;

namespace NTumbleBit.BouncyCastle.Crypto
{
	internal class OutputLengthException
		: DataLengthException
	{
		public OutputLengthException()
		{
		}

		public OutputLengthException(
			string message)
			: base(message)
		{
		}

		public OutputLengthException(
			string message,
			Exception exception)
			: base(message, exception)
		{
		}
	}
}
