using NBitcoin;
using NBitcoin.DataEncoders;
#if !CLIENT
using NTumbleBit.TumblerServer.Services;
#else
using NTumbleBit.Client.Tumbler.Services;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#if !CLIENT
namespace NTumbleBit.TumblerServer
#else
namespace NTumbleBit.Client.Tumbler
#endif
{
	public enum TransactionType
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
	}
	public class Tracker
	{
		class InternalRecord
		{

		}
		private readonly IRepository _Repo;

		public Tracker(IRepository repo)
		{
			if(repo == null)
				throw new ArgumentNullException("repo");
			_Repo = repo;
		}

		private static string GetCyclePartition(int cycleId)
		{
			return "Cycle_" + cycleId;
		}

		public void TransactionCreated(int cycleId, TransactionType type, uint256 txId)
		{
			var record = new TrackerRecord()
			{
				Cycle = cycleId,
				RecordType = RecordType.Transaction,
				TransactionType = type,
				TransactionId = txId
			};

			_Repo.UpdateOrInsert(GetCyclePartition(cycleId), txId.GetLow64().ToString(), record, (a, b) => b);
			_Repo.UpdateOrInsert("Search", "t:" + txId.ToString(), cycleId, (a, b) => b);
		}

		public void AddressCreated(int cycleId, TransactionType type, Script scriptPubKey)
		{
			var record = new TrackerRecord()
			{
				Cycle = cycleId,
				RecordType = RecordType.ScriptPubKey,
				TransactionType = type,
				ScriptPubKey = scriptPubKey
			};
			_Repo.UpdateOrInsert(GetCyclePartition(cycleId), Rand(), record, (a, b) => b);
			_Repo.UpdateOrInsert("Search", "t:" + scriptPubKey.Hash.ToString(), cycleId, (a, b) => b);
		}

		private string Rand()
		{
			return RandomUtils.GetUInt64().ToString();
		}

		public TrackerRecord Search(Script script)
		{
			var row = _Repo.Get<int>("Search", "t:" + script.Hash);
			if(row == 0)
				return null;
			return GetRecords(row).Where(r => r.RecordType == RecordType.ScriptPubKey && r.ScriptPubKey == script).FirstOrDefault();
		}

		public TrackerRecord Search(uint256 txId)
		{
			var row = _Repo.Get<int>("Search", "t:" + txId);
			if(row == 0)
				return null;
			return GetRecords(row).Where(r => r.RecordType == RecordType.Transaction && r.TransactionId == txId).FirstOrDefault();
		}

		public TrackerRecord[] GetRecords(int cycleId)
		{
			return _Repo.List<TrackerRecord>(GetCyclePartition(cycleId));
		}
	}
}
