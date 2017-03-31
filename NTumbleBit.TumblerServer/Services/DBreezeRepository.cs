﻿using DBreeze;
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
		private string _Folder;
		public DBreezeRepository(string folder)
		{
			if(folder == null)
				throw new ArgumentNullException(nameof(folder));
			if(!Directory.Exists(folder))
				Directory.CreateDirectory(folder);
			_Folder = folder;
		}

		private Dictionary<string, DBreezeEngineReference> _EnginesByParitionKey = new Dictionary<string, DBreezeEngineReference>();

		public void UpdateOrInsert<T>(string partitionKey, string rowKey, T data, Func<T, T, T> update)
		{
			lock(_EnginesByParitionKey)
			{
				var engine = GetEngine(partitionKey);
				using(var tx = engine.GetTransaction())
				{
					T newValue = data;
					var existingRow = tx.Select<string, byte[]>(GetTableName<T>(), rowKey);
					if(existingRow != null && existingRow.Exists)
					{
						var existing = Serializer.ToObject<T>(Encoding.UTF8.GetString(existingRow.Value));
						if(existing != null)
							newValue = update(existing, newValue);
					}
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
			DBreezeEngineReference engine;
			if(!_EnginesByParitionKey.TryGetValue(partitionKey, out engine))
			{
				engine = new DBreezeEngineReference() { PartitionKey = partitionKey, Engine = new DBreezeEngine(partitionPath) };
				_EnginesByParitionKey.Add(partitionKey, engine);
				_EngineReferences.Enqueue(engine);
			}
			engine.Used++;
			while(_EngineReferences.Count > MaxOpenedEngine)
			{
				var reference = _EngineReferences.Dequeue();
				reference.Used--;
				if(reference.Used <= 0 && reference != engine)
				{
					if(_EnginesByParitionKey.Remove(reference.PartitionKey))
						reference.Engine.Dispose();
				}
				else
				{
					_EngineReferences.Enqueue(reference);
				}
			}
			return engine.Engine;
		}

		Queue<DBreezeEngineReference> _EngineReferences = new Queue<DBreezeEngineReference>();
		
		public int OpenedEngine
		{
			get
			{
				lock(_EnginesByParitionKey)
				{				
					return _EngineReferences.Count;
				}
			}
		}
		public int MaxOpenedEngine
		{
			get;
			set;
		} = 10;

		class DBreezeEngineReference
		{
			public DBreezeEngine Engine
			{
				get; set;
			}
			public string PartitionKey
			{
				get;
				internal set;
			}
			public int Used
			{
				get; set;
			}
		}
		private string GetPartitionPath(string partitionKey)
		{
			return Path.Combine(_Folder, GetDirectory(partitionKey));
		}

		private string GetDirectory(string partitionKey)
		{
			return partitionKey;
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
				Utils.DeleteRecursivelyWithMagicDust(GetPartitionPath(partitionKey));
			}
		}

		public List<T> List<T>(string partitionKey)
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
				return result;
			}
		}

        public List<string> ListPartitionKeys()
        {
            List<string> paths = Directory.GetDirectories(_Folder, "*.*").ToList();
            for (var i = 0; i < paths.Count; i++)
            {
                int pos = paths[i].LastIndexOf("\\") + 1; // may need to check / for unix systems
                paths[i] = paths[i].Substring(pos, paths[i].Length - pos);
            }

            return paths;
        }

        private string GetTableName<T>()
		{
			return typeof(T).FullName;
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
					engine.Value.Engine.Dispose();
				}
				_EngineReferences.Clear();
				_EnginesByParitionKey.Clear();
			}
		}
	}
}
