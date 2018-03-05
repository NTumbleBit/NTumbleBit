using NTumbleBit.BouncyCastle.Crypto.Parameters;
using NTumbleBit.BouncyCastle.Math;
using System;
using System.Linq;

namespace TumbleBitSetup
{
    internal static class PermutationTest
    {
        /// <summary>
        /// Proving algorithm as specified in section 2.6.2 of the setup
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
            BigInteger Modulus = privKey.Modulus;

            var keyLength = Modulus.BitLength;
            var alpha = setup.Alpha;

            var psBytes = setup.PublicString;
            var k = setup.SecurityParameter;

            byte[][] sigs;

            // Generate m1 and m2
            Get_m1_m2((decimal)alpha, e.IntValue, k, out int m1, out int m2);

            // Calculate eN
            var eN = Modulus.Multiply(e);

            // Generate a pair (pub, sec) of keys with eN as e.
            var PrivatekeyPrime = Utils.GeneratePrivate(p, q, eN);

            // Extract public key (N, e) from private key.
            var pubKey = privKey.ToPublicKey();

            // Generate list of rho values
            GetRhos(m2, psBytes, pubKey, keyLength, out byte[][] rhoValues);

            // Signing the Rho values
            sigs = new byte[m2][];
            for (int i = 0; i < m2; i++)
            {
                if (i < m1)
                    sigs[i] = PrivatekeyPrime.Decrypt(rhoValues[i]);
                else
                    sigs[i] = privKey.Decrypt(rhoValues[i]);
            }
            return new PermutationTestProof(sigs);
        }

        /// <summary>
        /// Verifying algorithm as specified in section 2.6.3 of the setup
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

            BigInteger Modulus = pubKey.Modulus;
            BigInteger Exponent = pubKey.Exponent;

            // Check if exponent e is prime
            if (!Array.Exists(PermutationTestSetup.SPECIAL_E_VALUES, element => Exponent.IntValue == element))
                if (!Exponent.IsProbablePrime(k))
                    return false;

            // if N < 2^{KeySize-1} or N >= 2^{KeySize}
            if (Modulus.BitLength != keyLength)
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
                if (i < m1)
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
        /// Provide the check specified in step 4 of the verifying algorithm.
        /// </summary>
        /// <param name="alpha"> Prime number specified in the setup</param>
        /// <param name="N"> Modulus used in the public key</param>
        /// <returns>true if the check passes, false otherwise</returns>
        internal static bool CheckAlphaN(int alpha, BigInteger N)
        {
            foreach (int p in Utils.Primes(alpha - 1))
                if (N.Mod(BigInteger.ValueOf(p)).Equals(BigInteger.Zero))
                    return false;
            return true;
        }

        /// <summary>
        /// Generate the values m1 and m2 as specified in equation 2 of the setup.
        /// </summary>
        /// <param name="alpha">Prime number specified in the setup</param>
        /// <param name="e">Public Exponent used in the public key</param>
        /// <param name="k">Security parameter specified in the setup</param>
        internal static void Get_m1_m2(decimal alpha, int e, int k, out int m1, out int m2)
        {
            double p1 = -k / Math.Log(1.0 / ((double)alpha), 2.0);
            double p22 = 1.0 / ((double)alpha) + (1.0 / ((double)e)) * (1.0 - (1.0 / ((double)alpha)));
            double p2 = -k / Math.Log(p22, 2.0);
            m1 = (int)Math.Ceiling(p1);
            m2 = (int)Math.Ceiling(p2);
            return;
        }

        /// <summary>
        /// Generate a list of rho values as specified in section 2.6.4 in the setup*.
        /// <para/> *Please note that this function generates all rho values at once, not one by one as specified in the setup.
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
                int j = 1;
                // Combine the octet string
                var combined = Utils.Combine(keyBytes, psBytes, EI);
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