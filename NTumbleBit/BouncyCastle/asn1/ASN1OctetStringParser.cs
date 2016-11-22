using System.IO;

namespace NTumbleBit.BouncyCastle.Asn1
{
	internal interface Asn1OctetStringParser
		: IAsn1Convertible
	{
		Stream GetOctetStream();
	}
}
