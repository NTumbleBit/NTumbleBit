using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NTumbleBit.Tests
{
	public enum CoreNodeState
	{
		Stopped,
		Starting,
		Running,
		Killed
	}
	public class NodeConfigParameters : Dictionary<string, string>
	{
		public void Import(NodeConfigParameters configParameters)
		{
			foreach(var kv in configParameters)
			{
				if(!ContainsKey(kv.Key))
					Add(kv.Key, kv.Value);
			}
		}

		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();
			foreach(var kv in this)
				builder.AppendLine(kv.Key + "=" + kv.Value);
			return builder.ToString();
		}
	}
	public class NodeBuilder : IDisposable
	{
		public static NodeBuilder Create([CallerMemberNameAttribute]string caller = null)
		{
            var path = EnsureDownloaded(NodeDownloadData.Bitcoin.v0_16_3);
			try
			{
				Utils.DeleteRecursivelyWithMagicDust(caller);
			}
			catch(DirectoryNotFoundException)
			{
			}
			Directory.CreateDirectory(caller);
			return new NodeBuilder(caller, path);
		}

		private static string EnsureDownloaded(NodeDownloadData downloadData)
		{
            if(!Directory.Exists("TestData"))
                Directory.CreateDirectory("TestData");

            var osDownloadData = downloadData.GetCurrentOSDownloadData();
            var bitcoind = Path.Combine("TestData", String.Format(osDownloadData.Executable, downloadData.Version));
            var zip = Path.Combine("TestData", String.Format(osDownloadData.Archive, downloadData.Version));
            if(File.Exists(bitcoind))
                return bitcoind;

            string url = String.Format(osDownloadData.DownloadLink, downloadData.Version);
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10.0);
            var data = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
            CheckHash(osDownloadData, data);
            File.WriteAllBytes(zip, data);

            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ZipFile.ExtractToDirectory(zip, new FileInfo(zip).Directory.FullName);
            }
            else
            {
                Process.Start("tar", "-zxvf " + zip + " -C TestData").WaitForExit();
            }
            File.Delete(zip);
            return bitcoind;
        }

        private static void CheckHash(NodeOSDownloadData osDownloadData, byte[] data)
        {
            var actual = Encoders.Hex.EncodeData(Hashes.SHA256(data));
            if(!actual.Equals(osDownloadData.Hash, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Hash of downloaded file does not match (Expected: {osDownloadData.Hash}, Actual: {actual})");
        }

        private int last = 0;
		private string _Root;
		private string _Bitcoind;
		public NodeBuilder(string root, string bitcoindPath)
		{
			_Root = root;
			_Bitcoind = bitcoindPath;
		}

		public string BitcoinD
		{
			get
			{
				return _Bitcoind;
			}
		}


		private readonly List<CoreNode> _Nodes = new List<CoreNode>();
		public List<CoreNode> Nodes
		{
			get
			{
				return _Nodes;
			}
		}


		private readonly NodeConfigParameters _ConfigParameters = new NodeConfigParameters();
		public NodeConfigParameters ConfigParameters
		{
			get
			{
				return _ConfigParameters;
			}
		}

		public CoreNode CreateNode(bool start = false)
		{
			var child = Path.Combine(_Root, last.ToString());
			last++;
			try
			{
				Utils.DeleteRecursivelyWithMagicDust(child);
			}
			catch(DirectoryNotFoundException)
			{
			}
			var node = new CoreNode(child, this);
			Nodes.Add(node);
			if(start)
				node.Start();
			return node;
		}

		public void StartAll()
		{
			Task.WaitAll(Nodes.Where(n => n.State == CoreNodeState.Stopped).Select(n => n.StartAsync()).ToArray());
		}

		public void Dispose()
		{
			foreach(var node in Nodes)
				node.Kill();
			foreach(var disposable in _Disposables)
				disposable.Dispose();
		}

		private List<IDisposable> _Disposables = new List<IDisposable>();
		internal void AddDisposable(IDisposable group)
		{
			_Disposables.Add(group);
		}
	}

	public class CoreNode
	{
		private readonly NodeBuilder _Builder;
		private string _Folder;
		public string Folder
		{
			get
			{
				return _Folder;
			}
		}

		public IPEndPoint Endpoint
		{
			get
			{
				return new IPEndPoint(IPAddress.Parse("127.0.0.1"), ports[0]);
			}
		}

		public string Config
		{
			get
			{
				return _Config;
			}
		}

		private readonly NodeConfigParameters _ConfigParameters = new NodeConfigParameters();
		private string _Config;

		public NodeConfigParameters ConfigParameters
		{
			get
			{
				return _ConfigParameters;
			}
		}

		public CoreNode(string folder, NodeBuilder builder)
		{
			_Builder = builder;
			_Folder = folder;
			_State = CoreNodeState.Stopped;
			CleanFolder();
			Directory.CreateDirectory(folder);
			dataDir = Path.Combine(folder, "data");
			Directory.CreateDirectory(dataDir);
			var pass = Encoders.Hex.EncodeData(RandomUtils.GetBytes(20));
			creds = new NetworkCredential(pass, pass);
			_Config = Path.Combine(dataDir, "bitcoin.conf");
			ConfigParameters.Import(builder.ConfigParameters);
			ports = new int[2];
			FindPorts(ports);
		}

		private void CleanFolder()
		{
			try
			{
				Utils.DeleteRecursivelyWithMagicDust(_Folder);
			}
			catch(DirectoryNotFoundException) { }
		}
#if !NOSOCKET
		public void Sync(CoreNode node, bool keepConnection = false)
		{
			var rpc = CreateRPCClient();
			var rpc1 = node.CreateRPCClient();
			rpc.AddNode(node.Endpoint, true);
			while(rpc.GetBestBlockHash() != rpc1.GetBestBlockHash())
			{
				Thread.Sleep(200);
			}
			if(!keepConnection)
				rpc.RemoveNode(node.Endpoint);
		}
#endif
		private CoreNodeState _State;
		public CoreNodeState State
		{
			get
			{
				return _State;
			}
		}

		private int[] ports;

		public int ProtocolPort
		{
			get
			{
				return ports[0];
			}
		}
		public void Start()
		{
			StartAsync().GetAwaiter().GetResult();
		}

		private readonly NetworkCredential creds;
		public RPCClient CreateRPCClient()
		{
			return new RPCClient(creds, new Uri("http://127.0.0.1:" + ports[1] + "/"), Network.RegTest);
		}

		public RestClient CreateRESTClient()
		{
			return new RestClient(new Uri("http://127.0.0.1:" + ports[1] + "/"));
		}
#if !NOSOCKET
		public Node CreateNodeClient()
		{
			return Node.Connect(Network.RegTest, "127.0.0.1:" + ports[0]);
		}
		public Node CreateNodeClient(NodeConnectionParameters parameters)
		{
			return Node.Connect(Network.RegTest, "127.0.0.1:" + ports[0], parameters);
		}
#endif

		public async Task StartAsync()
		{
			NodeConfigParameters config = new NodeConfigParameters
			{
				{"regtest", "1"},
				{"rest", "1"},
				{"server", "1"},
				{"txindex", "0"},
				{"rpcuser", creds.UserName},
				{"rpcpassword", creds.Password},
				{"whitebind", "127.0.0.1:" + ports[0].ToString()},
				{"rpcport", ports[1].ToString()},
				{"printtoconsole", "1"},
				{"keypool", "10"}
			};
			config.Import(ConfigParameters);
			File.WriteAllText(_Config, config.ToString());
			lock(l)
			{
				_Process = Process.Start(new FileInfo(_Builder.BitcoinD).FullName, "-conf=bitcoin.conf" + " -datadir=" + dataDir + " -debug=net");
				_State = CoreNodeState.Starting;
			}
			while(true)
			{
				try
				{
					await CreateRPCClient().GetBlockHashAsync(0).ConfigureAwait(false);
					_State = CoreNodeState.Running;
					break;
				}
				catch { }
				if(_Process == null || _Process.HasExited)
					break;
			}
		}


		private Process _Process;
		private readonly string dataDir;

		private void FindPorts(int[] portArray)
		{
			int i = 0;
			while(i < portArray.Length)
			{
				var port = RandomUtils.GetUInt32() % 4000;
				port = port + 10000;
				if(portArray.Any(p => p == port))
					continue;
				try
				{
					TcpListener listener = new TcpListener(IPAddress.Loopback, (int)port);
					listener.Start();
					listener.Stop();
					portArray[i] = (int)port;
					i++;
				}
				catch(SocketException) { }
			}
		}

		private List<Transaction> transactions = new List<Transaction>();
		private HashSet<OutPoint> locked = new HashSet<OutPoint>();
		private Money fee = Money.Coins(0.0001m);
		public Transaction GiveMoney(Script destination, Money amount, bool broadcast = true)
		{
			var rpc = CreateRPCClient();
			TransactionBuilder builder = new TransactionBuilder();
			builder.AddKeys(rpc.ListSecrets().OfType<ISecret>().ToArray());
			builder.AddCoins(rpc.ListUnspent().Where(c => !locked.Contains(c.OutPoint)).Select(c => c.AsCoin()));
			builder.Send(destination, amount);
			builder.SendFees(fee);
			builder.SetChange(GetFirstSecret(rpc));
			var tx = builder.BuildTransaction(true);
			foreach(var outpoint in tx.Inputs.Select(i => i.PrevOut))
			{
				locked.Add(outpoint);
			}
			if(broadcast)
				Broadcast(tx);
			else
				transactions.Add(tx);
			return tx;
		}

		public void Rollback(Transaction tx)
		{
			transactions.Remove(tx);
			foreach(var outpoint in tx.Inputs.Select(i => i.PrevOut))
			{
				locked.Remove(outpoint);
			}

		}

#if !NOSOCKET
		public void Broadcast(Transaction transaction)
		{
			using(var node = CreateNodeClient())
			{
				node.VersionHandshake();
				node.SendMessageAsync(new InvPayload(transaction));
				node.SendMessageAsync(new TxPayload(transaction));
				node.PingPong();
			}
		}
#else
        public void Broadcast(Transaction transaction)
        {
            var rpc = CreateRPCClient();
            rpc.SendRawTransaction(transaction);
        }
#endif
		public void SelectMempoolTransactions()
		{
			var rpc = CreateRPCClient();
			var txs = rpc.GetRawMempool();

			var tasks = txs.Select(t => rpc.GetRawTransactionAsync(t)).ToArray();
			Task.WaitAll(tasks);
			transactions.AddRange(tasks.Select(t => t.GetAwaiter().GetResult()).ToArray());
		}

		public void Broadcast(Transaction[] txs)
		{
			foreach(var tx in txs)
				Broadcast(tx);
		}

		public void Split(Money amount, int parts)
		{
			var rpc = CreateRPCClient();
			TransactionBuilder builder = new TransactionBuilder();
			builder.AddKeys(rpc.ListSecrets().OfType<ISecret>().ToArray());
			builder.AddCoins(rpc.ListUnspent().Select(c => c.AsCoin()));
			var secret = GetFirstSecret(rpc);
			foreach(var part in (amount - fee).Split(parts))
			{
				builder.Send(secret, part);
			}
			builder.SendFees(fee);
			builder.SetChange(secret);
			var tx = builder.BuildTransaction(true);
			Broadcast(tx);
		}

		private object l = new object();
		public void Kill(bool cleanFolder = true)
		{
			lock(l)
			{
				if(_Process != null && !_Process.HasExited)
				{
					_Process.Kill();
					_Process.WaitForExit();
				}
				_State = CoreNodeState.Killed;
				if(cleanFolder)
					CleanFolder();
			}
		}

		public DateTimeOffset? MockTime
		{
			get;
			set;
		}

		public void SetMinerSecret(BitcoinSecret secret)
		{
			CreateRPCClient().ImportPrivKey(secret);
			MinerSecret = secret;
		}

		public BitcoinSecret MinerSecret
		{
			get;
			private set;
		}

		public Block[] Generate(int blockCount, bool includeUnbroadcasted = true, bool broadcast = true)
		{
			var rpc = CreateRPCClient();
			var blocks = rpc.Generate(blockCount);
			rpc = rpc.PrepareBatch();
			var tasks = blocks.Select(b => rpc.GetBlockAsync(b)).ToArray();
			rpc.SendBatch();
			return tasks.Select(b => b.GetAwaiter().GetResult()).ToArray();
		}

		private List<uint256> _ToMalleate = new List<uint256>();
		public void Malleate(uint256 txId)
		{
			_ToMalleate.Add(txId);
		}

		private Transaction DoMalleate(Transaction transaction)
		{
			transaction = transaction.Clone();
			if(!transaction.IsCoinBase)
				foreach(var input in transaction.Inputs)
				{
					List<Op> malleated = new List<Op>();
					foreach(var op in input.ScriptSig.ToOps())
					{
						try
						{
							var sig = new TransactionSignature(op.PushData);
							sig = MakeHighS(sig);
							malleated.Add(Op.GetPushOp(sig.ToBytes()));
						}
						catch { malleated.Add(op); }
					}
					input.ScriptSig = new Script(malleated.ToArray());
				}
			return transaction;
		}



		private TransactionSignature MakeHighS(TransactionSignature sig)
		{
			var curveOrder = new NBitcoin.BouncyCastle.Math.BigInteger("115792089237316195423570985008687907852837564279074904382605163141518161494337", 10);
			var ecdsa = new ECDSASignature(sig.Signature.R, sig.Signature.S.Negate().Mod(curveOrder));
			return new TransactionSignature(ecdsa, sig.SigHash);
		}

		public void BroadcastBlocks(Block[] blocks)
		{
			using(var node = CreateNodeClient())
			{
				node.VersionHandshake();
				BroadcastBlocks(blocks, node);
			}
		}

		public void BroadcastBlocks(Block[] blocks, Node node)
		{
			foreach(var block in blocks)
			{
				node.SendMessageAsync(new InvPayload(block));
				node.SendMessageAsync(new BlockPayload(block));
			}
			node.PingPong();
		}

		public Block[] FindBlock(int blockCount = 1, bool includeMempool = true)
		{
			SelectMempoolTransactions();
			return Generate(blockCount, includeMempool);
		}

		private class TransactionNode
		{
			public TransactionNode(Transaction tx)
			{
				Transaction = tx;
				Hash = tx.GetHash();
			}
			public uint256 Hash = null;
			public Transaction Transaction = null;
			public List<TransactionNode> DependsOn = new List<TransactionNode>();
		}

		private List<Transaction> Reorder(List<Transaction> txs)
		{
			if(txs.Count == 0)
				return txs;
			var result = new List<Transaction>();
			var dictionary = txs.ToDictionary(t => t.GetHash(), t => new TransactionNode(t));
			foreach(var transaction in dictionary.Select(d => d.Value))
			{
				foreach(var input in transaction.Transaction.Inputs)
				{
					var node = dictionary.TryGet(input.PrevOut.Hash);
					if(node != null)
					{
						transaction.DependsOn.Add(node);
					}
				}
			}
			while(dictionary.Count != 0)
			{
				foreach(var node in dictionary.Select(d => d.Value).ToList())
				{
					foreach(var parent in node.DependsOn.ToList())
					{
						if(!dictionary.ContainsKey(parent.Hash))
							node.DependsOn.Remove(parent);
					}
					if(node.DependsOn.Count == 0)
					{
						result.Add(node.Transaction);
						dictionary.Remove(node.Hash);
					}
				}
			}
			return result;
		}

		private BitcoinSecret GetFirstSecret(RPCClient rpc)
		{
			if(MinerSecret != null)
				return MinerSecret;
			var dest = rpc.ListSecrets().FirstOrDefault();
			if(dest == null)
			{
				var address = rpc.GetNewAddress();
				dest = rpc.DumpPrivKey(address);
			}
			return dest;
		}
	}


    public partial class NodeDownloadData
    {
        public string Version
        {
            get; set;
        }

        public NodeOSDownloadData Linux
        {
            get; set;
        }

        public NodeOSDownloadData Mac
        {
            get; set;
        }

        public NodeOSDownloadData Windows
        {
            get; set;
        }

        public static BitcoinNodeDownloadData Bitcoin
        {
            get; set;
        } = new BitcoinNodeDownloadData();

        public class BitcoinNodeDownloadData
        {
            public NodeDownloadData v0_13_2 = new NodeDownloadData()
            {
                Version = "0.13.1",
                Linux = new NodeOSDownloadData()
                {
                    Archive = "bitcoin-{0}-x86_64-linux-gnu.tar.gz",
                    DownloadLink = "https://bitcoin.org/bin/bitcoin-core-{0}/bitcoin-{0}-x86_64-linux-gnu.tar.gz",
                    Executable = "bitcoin-{0}/bin/bitcoind",
                    Hash = "29215a7fe7430224da52fc257686d2d387546eb8acd573a949128696e8761149"
                },
                Mac = new NodeOSDownloadData()
                {
                    Archive = "bitcoin-{0}-osx64.tar.gz",
                    DownloadLink = "https://bitcoin.org/bin/bitcoin-core-{0}/bitcoin-{0}-osx64.tar.gz",
                    Executable = "bitcoin-{0}/bin/bitcoind",
                    Hash = "8037b25310966127c589eb419534d7763ad62c2c29b94e0a37a5c5f5d96f541a"
                },
                Windows = new NodeOSDownloadData()
                {
                    Executable = "bitcoin-{0}/bin/bitcoind.exe",
                    DownloadLink = "https://bitcoin.org/bin/bitcoin-core-{0}/bitcoin-{0}-win32.zip",
                    Archive = "bitcoin-{0}-win32.zip",
                    Hash = "4d1c26675088219d8e2204b5a9f028916d5982db860298a70b6ed08e30af2a53"
                }
            };

            public NodeDownloadData v0_16_3 = new NodeDownloadData()
            {
                Version = "0.16.3",
                Linux = new NodeOSDownloadData()
                {
                    Archive = "bitcoin-{0}-x86_64-linux-gnu.tar.gz",
                    DownloadLink = "https://bitcoin.org/bin/bitcoin-core-{0}/bitcoin-{0}-x86_64-linux-gnu.tar.gz",
                    Executable = "bitcoin-{0}/bin/bitcoind",
                    Hash = "5d422a9d544742bc0df12427383f9c2517433ce7b58cf672b9a9b17c2ef51e4f"
                },
                Mac = new NodeOSDownloadData()
                {
                    Archive = "bitcoin-{0}-osx64.tar.gz",
                    DownloadLink = "https://bitcoin.org/bin/bitcoin-core-{0}/bitcoin-{0}-osx64.tar.gz",
                    Executable = "bitcoin-{0}/bin/bitcoind",
                    Hash = "78c3bff3b619a19aed575961ea43cc9e142959218835cf51aede7f0b764fc25d"
                },
                Windows = new NodeOSDownloadData()
                {
                    Executable = "bitcoin-{0}/bin/bitcoind.exe",
                    DownloadLink = "https://bitcoin.org/bin/bitcoin-core-{0}/bitcoin-{0}-win32.zip",
                    Archive = "bitcoin-{0}-win32.zip",
                    Hash = "e3d6a962a4c2cbbd4798f7257a0f85d54cec095e80d9b0f543f4c707b06c8839"
                }
            };
        }

        public NodeOSDownloadData GetCurrentOSDownloadData()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Windows :
                   RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? Linux :
                   RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Mac :
                   throw new NotSupportedException();
        }
    }

    public class NodeOSDownloadData
    {
        public string Archive
        {
            get; set;
        }
        public string DownloadLink
        {
            get; set;
        }
        public string Executable
        {
            get; set;
        }
        public string Hash
        {
            get; set;
        }
    }
}
