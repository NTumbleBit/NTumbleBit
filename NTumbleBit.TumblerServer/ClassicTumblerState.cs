using NBitcoin;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using NTumbleBit.TumblerServer;
using NTumbleBit.TumblerServer.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NTumbleBit.ClassicTumbler
{
	public class ClassicTumblerState
	{
		public List<ClassicTumble> Tumbles;
		private ClassicTumblerRepository Repo;

		public ClassicTumblerState(ClassicTumblerRepository repo)
		{
			Repo = repo;
			Tumbles = new List<ClassicTumble>();
			List<string> keys = Repo.Repository.ListPartitionKeys().Where(x => x.Contains("Tumble")).ToList();
			foreach (var key in keys)
			{
				Tumbles.Add(new ClassicTumble(
					Convert.ToInt32(key.Substring(key.LastIndexOf("_") + 1)),
					Repo.Repository.List<AlicePaymentChannel>(key),
					Repo.Repository.List<BobPaymentChannel>(key)
				));
			}
		}

		public void AddAliceChannel(int cycleId)
		{
			var tumble = Tumbles.FirstOrDefault(t => t.Cycle == cycleId);
			if (tumble != null)
			{
				tumble.Alices.Add(new AlicePaymentChannel());
			}
			else
			{
				tumble = new ClassicTumble(cycleId);
				tumble.Alices.Add(new AlicePaymentChannel());
				Tumbles.Add(tumble);
			}
		}

		public void ConfirmAliceChannel(int cycleId, uint256 txId)
		{
			var tumble = Tumbles.FirstOrDefault(t => t.Cycle == cycleId);
			if (tumble != null)
			{
				var alice = tumble.Alices.FirstOrDefault(a => a.TxId == null);
				alice.Confirm(txId);
				Repo.Save(cycleId, alice);
			}
		}

		public void AttemptCloseAliceChannel(int cycleId)
		{
			var tumble = Tumbles.FirstOrDefault(t => t.Cycle == cycleId);
			if (tumble != null)
			{
				var alice = tumble.Alices.FirstOrDefault(a => a.Status == PaymentChannelState.Open);
				alice.AttemptClose();
				Repo.Save(cycleId, alice);
			}
		}

		public void AddBobChannel(int cycleId)
		{
			var tumble = Tumbles.FirstOrDefault(t => t.Cycle == cycleId);
			if (tumble != null)
			{
				tumble.Bobs.Add(new BobPaymentChannel());
			}
			else
			{
				tumble = new ClassicTumble(cycleId);
				tumble.Bobs.Add(new BobPaymentChannel());
				Tumbles.Add(tumble);
			}
		}

		public void ConfirmBobChannel(int cycleId, uint256 txId)
		{
			var tumble = Tumbles.FirstOrDefault(t => t.Cycle == cycleId);
			if (tumble != null)
			{
				var bob = tumble.Bobs.FirstOrDefault(a => a.TxId == null);
				bob.Confirm(txId);
				Repo.Save(cycleId, bob);
			}
		}

		public void AttemptCloseBobChannel(int cycleId)
		{
			var tumble = Tumbles.FirstOrDefault(t => t.Cycle == cycleId);
			if (tumble != null)
			{
				var bob = tumble.Bobs.FirstOrDefault(b => b.Status == PaymentChannelState.Open);
				bob.AttemptClose();
				Repo.Save(cycleId, bob);
			}
		}
	}

	public class ClassicTumble
	{
		public int Cycle;
		public List<AlicePaymentChannel> Alices;
		public List<BobPaymentChannel> Bobs;

		public ClassicTumble(int cycleId)
		{
			Cycle = cycleId;
			Alices = new List<AlicePaymentChannel>();
			Bobs = new List<BobPaymentChannel>();
		}

		public ClassicTumble(int cycleId, List<AlicePaymentChannel> alices, List<BobPaymentChannel> bobs)
		{
			Cycle = cycleId;
			Alices = alices;
			Bobs = bobs;
		}


	}
	public enum PaymentChannelState
	{
		AcceptingOpen,
		Open,
		AttemptedClose,
		Closed
	}

	public class AlicePaymentChannel
	{
		public uint256 TxId
		{
			get; set;
		}

		public PaymentChannelState Status
		{
			get; set;
		}

		public int ETag
		{
			get; set;
		}

		public AlicePaymentChannel()
		{
			Status = PaymentChannelState.AcceptingOpen;
		}

		public void Confirm(uint256 txId)
		{
			TxId = txId;
			Status = PaymentChannelState.Open;
		}

		public void AttemptClose()
		{
			Status = PaymentChannelState.AttemptedClose;
		}
	}


	public class BobPaymentChannel
	{
		public uint256 TxId
		{
			get; set;
		}

		public PaymentChannelState Status
		{
			get; set;
		}

		public int ETag
		{
			get; set;
		}

		public BobPaymentChannel()
		{
			Status = PaymentChannelState.AcceptingOpen;
		}

		public void Confirm(uint256 txId)
		{
			TxId = txId;
			Status = PaymentChannelState.Open;
		}

		public void AttemptClose()
		{
			Status = PaymentChannelState.AttemptedClose;
		}
	}
}