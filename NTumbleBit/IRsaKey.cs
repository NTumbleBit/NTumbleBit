using NTumbleBit.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTumbleBit
{
	public interface IRsaKey
	{
		byte[] Blind(byte[] data, ref Blind blind);
		byte[] Unblind(byte[] data, Blind blind);
	}
	interface IRsaKeyPrivate
	{
		RsaKeyParameters Key
		{
			get;
		}
	}
}
