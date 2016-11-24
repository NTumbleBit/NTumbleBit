using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class Serializer
	{
		public Serializer(Stream stream, bool serializing)
		{
			if(stream == null)
				throw new ArgumentNullException("stream");
			_Serializing = serializing;
			_Inner = stream;
		}


		private readonly Stream _Inner;
		public Stream Inner
		{
			get
			{
				return _Inner;
			}
		}

		private readonly bool _Serializing;
		public bool Serializing
		{
			get
			{
				return _Serializing;
			}
		}
	}
}
