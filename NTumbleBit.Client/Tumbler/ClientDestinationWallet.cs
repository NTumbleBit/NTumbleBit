using NBitcoin;
using NTumbleBit.Client.Tumbler.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.Client.Tumbler
{
	public class ClientDestinationWallet
	{
		public class WalletDescription
		{
			public BitcoinExtPubKey Key
			{
				get; set;
			}
			public KeyPath DerivationPath
			{
				get; set;
			}
			public string Name
			{
				get; set;
			}
		}
		public ClientDestinationWallet(string walletName, BitcoinExtPubKey extPubKey, KeyPath derivationPath, IRepository repository)
		{
			walletName = walletName ?? "";
			repository.UpdateOrInsert(GetPartition(walletName), "", new WalletDescription()
			{
				Name = walletName,
				DerivationPath = derivationPath,
				Key = extPubKey
			}, (o, n) => n);
			_Repository = repository;
			_WalletName = walletName;
		}


		private readonly string _WalletName;
		public string WalletName
		{
			get
			{
				return _WalletName;
			}
		}

		WalletDescription GetWalletDescription()
		{
			return Repository.Get<WalletDescription>(GetPartition(_WalletName), "");
		}

		private readonly IRepository _Repository;
		public IRepository Repository
		{
			get
			{
				return _Repository;
			}
		}

		private string GetPartition(string walletName)
		{
			return "Wallet-" + walletName;
		}

		public Script GetNewDestination()
		{
			while(true)
			{
				var walletDescription = GetWalletDescription();
				var index = Repository.Get<int>(GetPartition(WalletName), "");
				var address = walletDescription.Key.ExtPubKey.Derive(walletDescription.DerivationPath).Derive((uint)index).PubKey.Hash.ScriptPubKey;
				index++;
				bool conflict = false;
				Repository.UpdateOrInsert(GetPartition(WalletName), "", index, (o, n) =>
				{
					conflict = o + 1 != n;
					return n;
				});
				if(conflict)
					continue;
				return address;
			}
		}
	}
}
