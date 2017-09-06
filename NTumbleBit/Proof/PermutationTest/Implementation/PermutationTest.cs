using System;
using System.Linq;
using System.Collections.Generic;
using NTumbleBit.BouncyCastle.Crypto.Parameters;
using NTumbleBit.BouncyCastle.Math;

namespace TumbleBitSetup
{
    // TODO: edit the page number references in the comments or remove them.
    internal static class PermutationTest
    {
        /// <summary>
        /// Proving Algorithm specified in page 7 (2.8.1) of the setup
        /// </summary>
        /// <param name="privKey">The secret key</param>
        /// <param name="setup">The setup parameters</param>
        /// <returns>List of signatures</returns>
        public static PermutationTestProof ProvePermutationTest(this RsaPrivateCrtKeyParameters privKey, PermutationTestSetup setup)
        {
            if (privKey == null)
                throw new ArgumentNullException(nameof(privKey));
            if (setup == null)
                throw new ArgumentNullException(nameof(setup));

            BigInteger p = privKey.P;
            BigInteger q = privKey.Q;
            BigInteger e = privKey.PublicExponent;

            int alpha = setup.Alpha;
            byte[] psBytes = setup.PublicString;
            int k = setup.SecurityParameter;



            byte[][] sigs;

            // Generate m1 and m2
            Get_m1_m2((decimal)alpha, e.IntValue, k, out int m1, out int m2);

            // Generate private and public keys
            BigInteger N = p.Multiply(q);
            var eN = N.Multiply(e);

            // Generate a pair (pub, priv) of keys for e and eN
            var keyPair = Utils.GeneratePrivate(p, q, e);
            var keyPrimePair = Utils.GeneratePrivate(p, q, eN);

            // Extract public key (N, e) from main key.
            var pubKey = (RsaKeyParameters)keyPair.Public;

            // Generate list of rho values
            GetRhos(m2, psBytes, pubKey, N.BitLength, out byte[][] rhoValues);

            // Signing the Rho values
            sigs = new byte[m2][];
            for (int i = 0; i < m2; i++)
            {
                if (i <= m1)
                    sigs[i] = ((RsaPrivateCrtKeyParameters)keyPrimePair.Private).Decrypt(rhoValues[i]);
                else
                    sigs[i] = ((RsaPrivateCrtKeyParameters)keyPair.Private).Decrypt(rhoValues[i]);
            }
            return new PermutationTestProof(sigs);
        }

        /// <summary>
        /// Verifying Algorithm specified in page 8 (2.8.2) of the setup
        /// </summary>
        /// <param name="pubKey">Public Key used to verify the proof</param>
        /// <param name="proof">Proof</param>
        /// <param name="setup">Setup parameters</param>
        /// <returns> true if the signatures verify, false otherwise</returns>
        public static bool VerifyPermutationTest(this RsaKeyParameters pubKey, PermutationTestProof proof, PermutationTestSetup setup)
        {
            if (setup == null)
                throw new ArgumentNullException(nameof(setup));
            if (proof == null)
                throw new ArgumentNullException(nameof(proof));

            byte[][] sigs = proof.Signatures;
            int alpha = setup.Alpha;
            int keyLength = setup.KeySize;
            byte[] psBytes = setup.PublicString;
            int k = setup.SecurityParameter;

            BigInteger Two = BigInteger.Two;
            var Modulus = pubKey.Modulus;
            var Exponent = pubKey.Exponent;
            BigInteger lowerLimit = Two.Pow(keyLength - 1);
            BigInteger upperLimit = Two.Pow(keyLength);

            // if N < 2^{KeySize-1}
            if (Modulus.CompareTo(lowerLimit) < 0)
                return false;

            // if N >= 2^{KeySize}
            if (Modulus.CompareTo(upperLimit) >= 0)
                return false;

            // Generate m1 and m2
            Get_m1_m2((decimal)alpha, Exponent.IntValue, k, out int m1, out int m2);

            // Verifying m2
            if (!m2.Equals(sigs.Length))
                return false;

            // Verify alpha and N
            if (!CheckAlphaN(alpha, Modulus))
                return false;

            // Generate a "weird" public key
            var eN = Modulus.Multiply(Exponent);
            var pubKeyPrime = new RsaKeyParameters(false, Modulus, eN);

            // Generate list of rho values
            GetRhos(m2, psBytes, pubKey, keyLength, out byte[][] rhoValues);

            // Verifying the signatures
            for (int i = 0; i < m2; i++)
            {
                if (i <= m1)
                {
                    var dec_sig = pubKeyPrime.Encrypt(sigs[i]);
                    if (!dec_sig.SequenceEqual(rhoValues[i]))
                        return false;
                }
                else
                {
                    var dec_sig = pubKey.Encrypt(sigs[i]);
                    if (!dec_sig.SequenceEqual(rhoValues[i]))
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Provides the check specified in step 3 of the verifying protocol.
        /// </summary>
        /// <param name="alpha"> Prime number specified in the setup</param>
        /// <param name="N"> Modulus used in the public key</param>
        /// <returns>true if the check passes, false otherwise</returns>
        internal static bool CheckAlphaN(int alpha, BigInteger N)
        {
            IEnumerable<int> primesList = Utils.Primes(alpha - 1);

            foreach (int p in primesList)
            {
                if (!(N.Gcd(BigInteger.ValueOf(p)).Equals(BigInteger.One)))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Generates the values m1 and m2 as specified in the "proving" protocol in section 2.8
        /// </summary>
        /// <param name="alpha">Prime number specified in the setup</param>
        /// <param name="e">Public Exponent used in the public key</param>
        /// <param name="k">Security parameter specified in the setup</param>
        internal static void Get_m1_m2(decimal alpha, int e, int k, out int m1, out int m2)
        {
            double p1 = -(k + 1) / Math.Log(1.0 / ((double)alpha), 2.0);
            double p22 = 1.0 / ((double)alpha) + (1.0 / ((double)e)) * (1.0 - (1.0 / ((double)alpha)));
            double p2 = -(k + 1) / Math.Log(p22, 2.0);
            m1 = (int)Math.Ceiling(p1);
            m2 = (int)Math.Ceiling(p2);
            return;
        }

        /// <summary>
        /// Generates a list of rho values as specified in the setup (2.8.1)
        /// </summary>
        /// <param name="m2">m2</param>
        /// <param name="psBytes">public string specified in the setup</param>
        /// <param name="key">Public key used</param>
        /// <param name="keyLength">The size of the RSA key in bits</param>
        internal static void GetRhos(int m2, byte[] psBytes, RsaKeyParameters key, int keyLength, out byte[][] rhoValues)
        {
            var m2Len = Utils.GetOctetLen(m2);
            rhoValues = new byte[m2][];
            BigInteger Modulus = key.Modulus;

            // ASN.1 encoding of the PublicKey
            var keyBytes = key.ToBytes();

            for (int i = 0; i < m2; i++)
            {
                // Byte representation of i
                var EI = Utils.I2OSP(i, m2Len);
                int j = 2;
                // Combine the octet string
                var combined = Utils.Combine(keyBytes, Utils.Combine(psBytes, EI));
                while (true)
                {
                    // OctetLength of j
                    var jLen = Utils.GetOctetLen(j);
                    // Byte representation of j
                    var EJ = Utils.I2OSP(j, jLen);
                    // Combine EJ with the rest of the string
                    var sub_combined = Utils.Combine(combined, EJ);
                    // Pass the bytes to H_1
                    byte[] ER = Utils.MGF1_SHA256(sub_combined, keyLength);
                    // Convert from Bytes to BigInteger
                    BigInteger input = Utils.OS2IP(ER);
                    // Check if the output is bigger or equal than N
                    if (input.CompareTo(Modulus) >= 0)
                    {
                        j++;
                        continue;
                    }
                    rhoValues[i] = ER;
                    break;
                }
            }
        }
    }

}