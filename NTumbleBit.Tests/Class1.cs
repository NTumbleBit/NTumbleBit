using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NTumbleBit.Tests
{
    public class Class1
    {
		[Fact]
		public void CanGenerateParseAndSaveRsaKey()
		{
			RsaKey key = new RsaKey();
			RsaKey key2  = new RsaKey(key.ToBytes());
			Assert.True(key.ToBytes().SequenceEqual(key2.ToBytes()));
			Assert.True(key.PubKey.ToBytes().SequenceEqual(key2.PubKey.ToBytes()));
			Assert.True(new RsaPubKey(key.PubKey.ToBytes()).ToBytes().SequenceEqual(key2.PubKey.ToBytes()));
		}
    }
}
