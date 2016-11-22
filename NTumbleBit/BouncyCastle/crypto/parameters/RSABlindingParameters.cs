using System;

using NTumbleBit.BouncyCastle.Math;

namespace NTumbleBit.BouncyCastle.Crypto.Parameters
{
	class RsaBlindingParameters
		: ICipherParameters
	{
		private readonly RsaKeyParameters publicKey;
		private readonly BigInteger blindingFactor;

		public RsaBlindingParameters(
			RsaKeyParameters publicKey,
			BigInteger blindingFactor)
		{
			if(publicKey.IsPrivate)
				publicKey = new RsaKeyParameters(false, publicKey.Modulus, ((RsaPrivateCrtKeyParameters)publicKey).PublicExponent);

			this.publicKey = publicKey;
			this.blindingFactor = blindingFactor;
		}

		public RsaKeyParameters PublicKey
		{
			get
			{
				return publicKey;
			}
		}

		public BigInteger BlindingFactor
		{
			get
			{
				return blindingFactor;
			}
		}
	}
}