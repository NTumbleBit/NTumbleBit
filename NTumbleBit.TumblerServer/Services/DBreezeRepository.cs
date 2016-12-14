using DBreeze;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if !CLIENT
namespace NTumbleBit.TumblerServer.Services
#else
namespace NTumbleBit.Client.Tumbler.Services
#endif
{
	public class DBreezeRepository : IRepository, IDisposable
	{
		string _Folder;
		public DBreezeRepository(string folder)
		{
			if(folder == null)
				throw new ArgumentNullException("folder");
			_Folder = folder;
		}

		Dictionary<string, DBreezeEngine> _EnginesByParitionKey = new Dictionary<string, DBreezeEngine>();

		public void UpdateOrInsert<T>(string partitionKey, string rowKey, T data, Func<T, T, T> update)
		{
			lock(_EnginesByParitionKey)
			{
				var engine = GetEngine(partitionKey);
				using(var tx = engine.GetTransaction())
				{
					var existing = Get<T>(rowKey, tx);
					var newValue = data;
					if(existing != null)
						newValue = update(existing, newValue);
					var bytes = Encoding.UTF8.GetBytes(Serializer.ToString(newValue));
					tx.Insert(GetTableName<T>(), rowKey, bytes);
					tx.Commit();
				}
			}
		}

		private DBreezeEngine GetEngine(string partitionKey)
		{
			if(!Directory.Exists(_Folder))
				Directory.CreateDirectory(_Folder);
			string partitionPath = GetPartitionPath(partitionKey);
			if(!Directory.Exists(partitionPath))
				Directory.CreateDirectory(partitionPath);
			DBreezeEngine engine;
			if(!_EnginesByParitionKey.TryGetValue(partitionKey, out engine))
			{
				engine = new DBreezeEngine(partitionPath);
				_EnginesByParitionKey.Add(partitionKey, engine);
			}
			return engine;
		}

		private string GetPartitionPath(string partitionKey)
		{
			return Path.Combine(_Folder, GetDirectory(partitionKey));
		}

		private string GetDirectory(string partitionKey)
		{
			return Encoders.Hex.EncodeData(Hashes.SHA256(Encoding.UTF8.GetBytes(partitionKey)));
		}

		public void Delete(string partitionKey)
		{
			lock(_EnginesByParitionKey)
			{
				if(!_EnginesByParitionKey.ContainsKey(partitionKey))
					return;

				var engine = GetEngine(partitionKey);
				engine.Dispose();
				_EnginesByParitionKey.Remove(partitionKey);
				Directory.Delete(GetPartitionPath(partitionKey), true);
			}
		}

		public T[] List<T>(string partitionKey)
		{
			lock(_EnginesByParitionKey)
			{
				List<T> result = new List<T>();
				var engine = GetEngine(partitionKey);
				using(var tx = engine.GetTransaction())
				{
					foreach(var row in tx.SelectForward<string, byte[]>(GetTableName<T>()))
					{
						result.Add(Serializer.ToObject<T>(Encoding.UTF8.GetString(row.Value)));
					}
				}
				return result.ToArray();
			}
		}

		private string GetTableName<T>()
		{
			return typeof(T).Name;
		}

		public T Get<T>(string partitionKey, string rowKey)
		{
			lock(_EnginesByParitionKey)
			{
				var engine = GetEngine(partitionKey);
				using(var tx = engine.GetTransaction())
				{
					return Get<T>(rowKey, tx);
				}
			}
		}

		private T Get<T>(string rowKey, DBreeze.Transactions.Transaction tx)
		{
			var row = tx.Select<string, byte[]>(GetTableName<T>(), rowKey);
			if(row == null || !row.Exists)
				return default(T);
			return Serializer.ToObject<T>(Encoding.UTF8.GetString(row.Value));
		}

		public void Delete<T>(string partitionKey, string rowKey)
		{
			lock(_EnginesByParitionKey)
			{
				var engine = GetEngine(partitionKey);
				using(var tx = engine.GetTransaction())
				{
					tx.RemoveKey(GetTableName<T>(), rowKey);
					tx.Commit();
				}
			}
		}

		public void Dispose()
		{
			lock(_EnginesByParitionKey)
			{
				foreach(var engine in _EnginesByParitionKey)
				{
					engine.Value.Dispose();
				}
				_EnginesByParitionKey.Clear();
			}
		}
	}
}
