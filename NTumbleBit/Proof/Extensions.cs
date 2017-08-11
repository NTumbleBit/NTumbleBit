using NTumbleBit.BouncyCastle.Asn1;
using NTumbleBit.BouncyCastle.Asn1.Pkcs;
using NTumbleBit.BouncyCastle.Asn1.X509;
using NTumbleBit.BouncyCastle.Crypto.Engines;
using NTumbleBit.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TumbleBitSetup
{
    public static class Extensions
    {
        /// <summary>
        /// Preforms RSA decryption (or signing) using private key.
        /// </summary>
        /// <param name="privKey">The private key</param>
        /// <param name="encrypted">Data to decrypt (or sign)</param>
        /// <returns></returns>
        internal static byte[] Decrypt(this RsaPrivateCrtKeyParameters privKey, byte[] encrypted)
        {
            if(encrypted == null)
                throw new ArgumentNullException(nameof(encrypted));

            RsaEngine engine = new RsaEngine();
            engine.Init(false, privKey);

            return engine.ProcessBlock(encrypted, 0, encrypted.Length);
        }

        /// <summary>
        /// Preforms RSA encryption using public key.
        /// </summary>
        /// <param name="pubKey">Public key</param>
        /// <param name="data">Data to encrypt</param>
        /// <returns></returns>
        internal static byte[] Encrypt(this RsaKeyParameters pubKey, byte[] data)
        {
            if(data == null)
                throw new ArgumentNullException(nameof(data));

            RsaEngine engine = new RsaEngine();
            engine.Init(true, pubKey);

            return engine.ProcessBlock(data, 0, data.Length);
        }

        internal static byte[] ToBytes(this RsaKeyParameters pubKey)
        {
            RsaPublicKeyStructure keyStruct = new RsaPublicKeyStructure(
                pubKey.Modulus,
                pubKey.Exponent);
            var privInfo = new PrivateKeyInfo(AlgID, keyStruct.ToAsn1Object());
            return privInfo.ToAsn1Object().GetEncoded();
        }
        internal static AlgorithmIdentifier AlgID = new AlgorithmIdentifier(new DerObjectIdentifier("1.2.840.113549.1.1.1"), DerNull.Instance);

        internal static RsaKeyParameters ToPublicKey(this RsaPrivateCrtKeyParameters s)
        {
            return new RsaKeyParameters(false, s.Modulus, s.PublicExponent);
        }
    }
}
