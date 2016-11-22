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
	}
	interface IRsaKeyPrivate
	{
		RsaKeyParameters Key
		{
			get;
		}
	}
}
