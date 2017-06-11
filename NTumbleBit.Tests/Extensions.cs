﻿using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace NTumbleBit.Tests
{
    public static class Extensions
    {
		public static void AssertKnown(this NTumbleBit.Client.Tumbler.Tracker tracker, NTumbleBit.Client.Tumbler.TransactionType type, Script script)
		{
			var result = tracker.Search(script);
			Assert.True(result != null && result.TransactionType == type);
		}

		public static void AssertNotKnown(this NTumbleBit.Client.Tumbler.Tracker tracker, Script script)
		{
			var result = tracker.Search(script);
			Assert.Null(result);
		}

		public static void AssertKnown(this NTumbleBit.Client.Tumbler.Tracker tracker, NTumbleBit.Client.Tumbler.TransactionType type, uint256 tx)
		{
			var result = tracker.Search(tx);
			Assert.True(result != null && result.TransactionType == type);
		}

		public static void AssertNotKnown(this NTumbleBit.Client.Tumbler.Tracker tracker, uint256 tx)
		{
			var result = tracker.Search(tx);
			Assert.Null(result);
		}



		public static void AssertKnown(this NTumbleBit.TumblerServer.Tracker tracker, NTumbleBit.TumblerServer.TransactionType type, Script script)
		{
			var result = tracker.Search(script);
			Assert.True(result != null && result.TransactionType == type);
		}

		public static void AssertNotKnown(this NTumbleBit.TumblerServer.Tracker tracker, Script script)
		{
			var result = tracker.Search(script);
			Assert.Null(result);
		}

		public static void AssertKnown(this NTumbleBit.TumblerServer.Tracker tracker, NTumbleBit.TumblerServer.TransactionType type, uint256 tx)
		{
			var result = tracker.Search(tx);
			Assert.True(result != null && result.TransactionType == type);
		}

		public static void AssertNotKnown(this NTumbleBit.TumblerServer.Tracker tracker, uint256 tx)
		{
			var result = tracker.Search(tx);
			Assert.Null(result);
		}
	}
}