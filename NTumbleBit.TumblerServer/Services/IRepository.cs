using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#if !CLIENT
namespace NTumbleBit.TumblerServer.Services
#else
namespace NTumbleBit.Client.Tumbler.Services
#endif
{	
	public interface IRepository
	{
		void UpdateOrInsert<T>(string partitionKey, string rowKey, T data, Func<T, T, T> update);
		T[] List<T>(string partitionKey);
		void Delete<T>(string partitionKey, string rowKey);
		void Delete(string partitionKey);
		T Get<T>(string partitionKey, string rowKey);
	}
}
