using NTumbleBit.BouncyCastle.Crypto.Engines;
using NTumbleBit.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public class Puzzle
	{
		private readonly byte[] z;

		public Puzzle(byte[] z)
		{
			this.z = z;
		}

		//public Puzzle Blind(RsaKey key)
		//{
		//	var engine = new RsaBlindingEngine();
		//	RsaBlindingParameters p = new RsaBlindingParameters(key.PubKey, null);
		//	engine.Init()
		//}
	}
}
