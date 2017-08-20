using NBitcoin;
using Microsoft.Extensions.Logging;
using NBitcoin.DataEncoders;
using NTumbleBit.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.Services
{
	public enum TransactionType : int
	{
		TumblerEscrow,
		TumblerRedeem,
		/// <summary>
		/// The transaction that cashout tumbler's escrow (go to client)
		/// </summary>
		TumblerCashout,

		ClientEscrow,
		ClientRedeem,
		ClientOffer,
		ClientEscape,
		/// <summary>
		/// The transaction that cashout client's escrow (go to tumbler)
		/// </summary>
		ClientFulfill,
		ClientOfferRedeem
	}

	public enum RecordType
	{
		Transaction,
		ScriptPubKey
	}

	public class TrackerRecord
	{
		public TrackerRecord()
		{

		}

		public int Cycle
		{
			get; set;
		}
		public RecordType RecordType
		{
			get; set;
		}
		public TransactionType TransactionType
		{
			get; set;
		}
		public uint256 TransactionId
		{
			get;
			set;
		}
		public Script ScriptPubKey
		{
			get; set;
		}
		public CorrelationId Correlation
		{
			get;
			set;
		}
	}
	public class Tracker
	{
		class InternalRecord
		{

		}
		private readonly IRepository _Repo;

		public Tracker(IRepository repo, Network network)
		{
			if(repo == null)
				throw new ArgumentNullException("repo");
			if(network == null)
				throw new ArgumentNullException("network");
			_Repo = repo;
			_Network = network;
		}


		private readonly Network _Network;
		public Network Network
		{
			get
			{
				return _Network;
			}
		}

		private static string GetCyclePartition(int cycleId)
		{
			return "Cycle_" + cycleId;
		}

		public void TransactionCreated(int cycleId, TransactionType type, uint256 txId, CorrelationId correlation)
		{
			var record = new TrackerRecord()
			{
				Cycle = cycleId,
				RecordType = RecordType.Transaction,
				TransactionType = type,
				TransactionId = txId,
				Correlation = correlation,
			};

			bool isNew = true;

			//The 
			var uniqueKey = NBitcoin.Crypto.Hashes.Hash256(txId.ToBytes().Concat(correlation.ToBytes()).ToArray()).GetLow64();
			_Repo.UpdateOrInsert(GetCyclePartition(cycleId), uniqueKey.ToString(), record, (a, b) =>
			{
				isNew = false;
				return b;
			});
			_Repo.UpdateOrInsert("Search", "t:" + txId.ToString(), cycleId, (a, b) => b);

			if(isNew)
				Logs.Tracker.LogInformation($"Tracking transaction {type} of cycle {cycleId} with correlation {correlation.ToString(false)} ({txId})");
		}

		public void AddressCreated(int cycleId, TransactionType type, Script scriptPubKey, CorrelationId correlation)
		{
			var record = new TrackerRecord()
			{
				Cycle = cycleId,
				RecordType = RecordType.ScriptPubKey,
				TransactionType = type,
				ScriptPubKey = scriptPubKey,
				Correlation = correlation
			};

			bool isNew = true;
			_Repo.UpdateOrInsert(GetCyclePartition(cycleId), Rand(), record, (a, b) =>
			{
				isNew = false;
				return b;
			});
			_Repo.UpdateOrInsert("Search", "t:" + scriptPubKey.Hash.ToString(), cycleId, (a, b) => b);

			if(isNew)
				Logs.Tracker.LogInformation($"Tracking address {type} of cycle {cycleId} with correlation {correlation.ToString(false)} ({scriptPubKey.GetDestinationAddress(Network)})");
		}

		private string Rand()
		{
			return RandomUtils.GetUInt64().ToString();
		}

		public TrackerRecord[] Search(Script script)
		{
			var row = _Repo.Get<int>("Search", "t:" + script.Hash);
			if(row == 0)
				return new TrackerRecord[0];
			return GetRecords(row).Where(r => r.RecordType == RecordType.ScriptPubKey && r.ScriptPubKey == script).ToArray();
		}

		public TrackerRecord[] Search(uint256 txId)
		{
			var row = _Repo.Get<int>("Search", "t:" + txId);
			if(row == 0)
				return new TrackerRecord[0];
			return GetRecords(row).Where(r => r.RecordType == RecordType.Transaction && r.TransactionId == txId).ToArray();
		}

		public TrackerRecord[] GetRecords(int cycleId)
		{
			return _Repo.List<TrackerRecord>(GetCyclePartition(cycleId));
		}
	}
}
