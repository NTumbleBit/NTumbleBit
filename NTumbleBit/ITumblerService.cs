using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public interface ITumblerService
	{
		string Name
		{
			get;
		}
		void Start();
		Task Stop();
		bool Started
		{
			get;
		}
	}
}
