using System;
using System.Collections.Generic;
using System.Text;

namespace NTumbleBit.ClassicTumbler.CLI
{
    public class Stats
    {
		public int BobCount;
		public int AliceCount;
		public int CashoutCount;
		public int UncooperativeCount;
		public int CorrelationGroupCount;

		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();
			if(BobCount != 0)
				builder.AppendLine("Bob count: " + BobCount);
			if(AliceCount != 0)
				builder.AppendLine("Alice count: " + AliceCount);
			if(CashoutCount != 0)
				builder.AppendLine("Cashout count: " + CashoutCount);
			if(UncooperativeCount != 0)
				builder.AppendLine("Uncooperative count: " + UncooperativeCount);
			if(CorrelationGroupCount != 0)
				builder.AppendLine("Correlation group count: " + CorrelationGroupCount);
			return builder.ToString();
		}

		public static Stats operator +(Stats a, Stats b)
		{
			return new Stats()
			{
				AliceCount = a.AliceCount + b.AliceCount,
				BobCount = a.BobCount + b.BobCount,
				CashoutCount = a.CashoutCount + b.CashoutCount,
				UncooperativeCount = a.UncooperativeCount + b.UncooperativeCount,
				CorrelationGroupCount = a.CorrelationGroupCount + b.CorrelationGroupCount
			};
		}
	}
}
