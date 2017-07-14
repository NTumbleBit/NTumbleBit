using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace NTumbleBit
{
	public class NullDisposable : IDisposable
	{
		readonly static IDisposable _Instance = new NullDisposable();
		public static IDisposable Instance
		{
			get
			{
				return _Instance;
			}
		}
		private NullDisposable()
		{

		}
		public void Dispose()
		{

		}
	}
	public class ProcessDisposable : IDisposable
	{
		private Process process;

		public ProcessDisposable(Process process)
		{
			this.process = process;
		}

		public void Dispose()
		{
			try
			{
				if(!process.HasExited)
				{
					process.Kill();
					process.WaitForExit();
				}
			}
			catch { }
		}
	}
}
