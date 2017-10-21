using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Xunit;
using NTumbleBit.Services;

namespace NTumbleBit.Tests
{
    public static class Extensions
    {
		public static void AssertKnown(this Tracker tracker, TransactionType type, Script script)
		{
			var result = tracker.Search(script);
			Assert.Contains(result, r => r.TransactionType == type);
		}

		public static void AssertNotKnown(this Tracker tracker, Script script)
		{
			var result = tracker.Search(script);
			Assert.True(result.Length == 0);
		}

		public static void AssertKnown(this Tracker tracker, TransactionType type, uint256 tx)
		{
			var result = tracker.Search(tx);
			Assert.Contains(result, r => r.TransactionType == type);
		}

		public static void AssertNotKnown(this Tracker tracker, uint256 tx)
		{
			var result = tracker.Search(tx);
			Assert.True(result.Length == 0);
		}
	}
}
