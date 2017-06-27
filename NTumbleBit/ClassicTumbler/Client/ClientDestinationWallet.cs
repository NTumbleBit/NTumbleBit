using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NTumbleBit.Logging;
using NTumbleBit.Services;
using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler.Client
{
	public interface IDestinationWallet
	{
		Script GetNewDestination();
		KeyPath GetKeyPath(Script script);
	}
	public class ClientDestinationWallet : IDestinationWallet
	{
		ExtPubKey _ExtPubKey;
		KeyPath _DerivationPath;
		Network _Network;

		public ClientDestinationWallet(BitcoinExtPubKey extPubKey, KeyPath derivationPath, IRepository repository, Network network)
		{
			if(derivationPath == null)
				throw new ArgumentNullException("derivationPath");
			if(extPubKey == null)
				throw new ArgumentNullException("extPubKey");
			if(repository == null)
				throw new ArgumentNullException("repository");
			if(network == null)
				throw new ArgumentNullException("network");
			_Network = network;
			_Repository = repository;
			_ExtPubKey = extPubKey.ExtPubKey.Derive(derivationPath);
			_DerivationPath = derivationPath;
			_WalletId = "Wallet_" + Encoders.Base58.EncodeData(Hashes.Hash160(Encoding.UTF8.GetBytes(_ExtPubKey.ToString() + "-" + derivationPath.ToString())).ToBytes());
		}

		private readonly IRepository _Repository;
		private readonly string _WalletId;

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
				var index = Repository.Get<uint>(_WalletId, "");
				var address = _ExtPubKey.Derive((uint)index).PubKey.Hash.ScriptPubKey;
				index++;
				bool conflict = false;
				Repository.UpdateOrInsert(_WalletId, "", index, (o, n) =>
				{
					conflict = o + 1 != n;
					return n;
				});
				if(conflict)
					continue;
				Repository.UpdateOrInsert<uint?>(_WalletId, address.Hash.ToString(), (uint)(index - 1), (o, n) => n);

				var path = _DerivationPath.Derive((uint)(index - 1));
				Logs.Wallet.LogInformation($"Created address {address.GetDestinationAddress(_Network)} of with HD path {path}");
				return address;
			}
		}

		public KeyPath GetKeyPath(Script script)
		{
			var index = Repository.Get<uint?>(_WalletId, script.Hash.ToString());
			if(index == null)
				return null;
			return _DerivationPath.Derive(index.Value);
		}
	}
}
