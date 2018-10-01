using NTumbleBit.BouncyCastle.Crypto.Parameters;
using NTumbleBit.BouncyCastle.Math;
using NTumbleBit.BouncyCastle.Security;
using System;

namespace TumbleBitSetup
{
    internal static class PoupardStern
    {
        /// <summary>
        /// Proving algorithm as specified in section 3.2.1 of the setup.
        /// </summary> 
        /// <param name="privKey">The secret key</param>
        /// <param name="setup">The setup parameters</param>
        /// <returns>The PoupardStern proof</returns>
        public static PoupardSternProof ProvePoupardStern(this RsaPrivateCrtKeyParameters privKey, PoupardSternSetup setup)
        {
            if (privKey == null)
                throw new ArgumentNullException(nameof(privKey));
            if (setup == null)
                throw new ArgumentNullException(nameof(setup));

            BigInteger p = privKey.P;
            BigInteger q = privKey.Q;
            BigInteger Modulus = privKey.Modulus;

            int k = setup.SecurityParameter;
            var psBytes = setup.PublicString;
            int keyLength = setup.KeySize;

            BigInteger Two = BigInteger.Two;
            // 2^{|N| - 1}
            BigInteger lowerLimit = Two.Pow(keyLength - 1);

            // Rounding up k to the closest multiple of 8
            k = Utils.GetByteLength(k) * 8;

            // if N < 2^{KeySize-1} or N >= 2^{KeySize}
            if (Modulus.BitLength != keyLength)
                throw new ArgumentOutOfRangeException("RSA modulus value is out of range");

            // if even
            if ((Modulus.IntValue & 1) == 0)
                throw new ArgumentException("RSA modulus is even");

            // p and q don't produce a modulus N that has the expected bitLength
            if (!(Modulus.BitLength.Equals(keyLength)))
                throw new ArgumentException("RSA P and Q are bad");

            // Calculating phi
            BigInteger pSub1 = p.Subtract(BigInteger.One);
            BigInteger qSub1 = q.Subtract(BigInteger.One);
            BigInteger phi = pSub1.Multiply(qSub1);
            // N - phi(N)
            BigInteger NsubPhi = Modulus.Subtract(phi);

            // if 2^{|N|-1}/{(N-phi)*2^k} <= 2^k
            var p11 = Two.Pow(k);
            var p1 = lowerLimit.Divide(NsubPhi.Multiply(p11));
            if (p1.CompareTo(p11) <= 0)
                throw new ArgumentOutOfRangeException(nameof(Modulus), "Bad RSA modulus N");

            // Generate K
            GetK(k, out int BigK);

            // Extract public key (N, e) from private key.
            var pubKey = privKey.ToPublicKey();

            // Initialize and generate list of z values
            BigInteger[] zValues = new BigInteger[BigK];
            for (int i = 0; i < BigK; i++)
                zValues[i] = SampleFromZnStar(pubKey, psBytes, i, BigK, keyLength);
            BigInteger y;
            for (; ; )
            {
                // Initialize list of x values.
                BigInteger[] xValues = new BigInteger[BigK];

                // Get r
                GetR(keyLength, out BigInteger r);

                for (int i = 0; i < BigK; i++)
                    // Compute x_i
                    xValues[i] = zValues[i].ModPow(r, Modulus);

                // Compute w
                GetW(pubKey, psBytes, xValues, k, keyLength, out BigInteger w);

                // Compute y
                y = r.Add(NsubPhi.Multiply(w));

                // if y >= 2^{ |N| - 1 }
                if (y.CompareTo(lowerLimit) >= 0)
                    continue;

                // if y < 0
                if (y.CompareTo(BigInteger.Zero) < 0)
                    continue;

                return new PoupardSternProof(new Tuple<BigInteger[], BigInteger>(xValues, y));
            }

        }

        /// <summary>
        /// Verifying Algorithm as specified in section 3.3 of the setup.
        /// </summary>
        /// <param name="pubKey">Public key used</param>
        /// <param name="proof">The proof.</param>
        /// <param name="setup">Setup parameters.</param>
        /// <returns>true if the proof verifies, false otherwise</returns>
        public static bool VerifyPoupardStern(this RsaKeyParameters pubKey, PoupardSternProof proof, PoupardSternSetup setup)
        {
            if (pubKey == null)
                throw new ArgumentNullException(nameof(pubKey));
            if (proof == null)
                throw new ArgumentNullException(nameof(proof));
            if (setup == null)
                throw new ArgumentNullException(nameof(setup));

            int keyLength = setup.KeySize;
            int k = setup.SecurityParameter;
            var y = proof.YValue;

            BigInteger rPrime;

            // Rounding up k to the closest multiple of 8
            k = Utils.GetByteLength(k) * 8;

            var Modulus = pubKey.Modulus;
            var Exponent = pubKey.Exponent;

            // Checking that:
            // if y >= 2^{ |N| - 1 }
            if (y.BitLength >= keyLength)
                // TODO: Verify that this bit check is equivalent to the math check
                return false;
            // if y < 0
            if (y.CompareTo(BigInteger.Zero) < 0)
                return false;
            // if N < 2^{KeySize-1} or N >= 2^{KeySize}
            if (Modulus.BitLength != keyLength)
                return false;
            // if even
            if ((Modulus.IntValue & 1) == 0)
                return false;


            // Computing K
            GetK(k, out int BigK);

            // Check if the number of x_values is not equal to K
            if (proof.XValues.Length != BigK)
                return false;

            // Get w
            GetW(pubKey, setup.PublicString, proof.XValues, k, keyLength, out BigInteger w);

            // Computing rPrime
            rPrime = y.Subtract(Modulus.Multiply(w));

            // Verifying x values
            Dictionary<int, bool> numbers = new Dictionary<int, bool>();
            for (int i = 0; i < BigK; i++)
                numbers.Add(i, true);

            Parallel.ForEach(numbers, (n) =>
            {
                int i = n.Key;

                var z_i = SampleFromZnStar(pubKey, setup.PublicString, i, BigK, keyLength);
                // Compute right side of the equality
                var rs = z_i.ModPow(rPrime, Modulus);
                // If the two sides are not equal
                if (!(proof.XValues[i].Equals(rs)))
                    numbers[i] = false;
            });

            return !numbers.ContainsValue(false);
        }

        /// <summary>
        /// Generate z_i value as specified in section 3.3.1 of the setup.
        /// </summary>
        /// <param name="pubKey">Public key used</param>
        /// <param name="ps">public string specified in the setup</param>
        /// <param name="i">index i</param>
        /// <param name="k">Security parameter specified in the setup.</param>
        /// <param name="keyLength">The size of the RSA key in bits</param>
        /// <returns></returns>
        internal static BigInteger SampleFromZnStar(RsaKeyParameters pubKey, byte[] psBytes, int i, int BigK, int keyLength)
        {
            BigInteger Modulus = pubKey.Modulus;

            // Octet Length of i
            int iLen = Utils.GetOctetLen(BigK);
            // OctetString of i
            var EI = Utils.I2OSP(i, iLen);
            // ASN.1 encoding of the PublicKey
            var keyBytes = pubKey.ToBytes();
            // Combine the OctetString
            var combined = Utils.Combine(keyBytes, psBytes, EI);
            int j = 2;
            for (; ; )
            {
                // OctetLength of j
                var jLen = Utils.GetOctetLen(j);
                // OctetString of j
                var EJ = Utils.I2OSP(j, jLen);
                // Combine EJ with the rest of the string
                var sub_combined = Utils.Combine(combined, EJ);
                // Pass the bytes to H_1
                byte[] ER = Utils.MGF1_SHA256(sub_combined, keyLength);
                // Convert from OctetString to BigInteger
                BigInteger z_i = Utils.OS2IP(ER);
                // Check if the output is larger or equal to N OR GCD(z_i, N) != 1
                if (z_i.CompareTo(Modulus) >= 0 || !(z_i.Gcd(Modulus).Equals(BigInteger.One)))
                {
                    j++;
                    continue;
                }
                return z_i;
            }
        }

        /// <summary>
        /// Calculate the value of w as specified in section 3.3.2 of the setup.
        /// </summary>
        /// <param name="pubKey">Public key used</param>
        /// <param name="ps">public string specified in the setup</param>
        /// <param name="xValues"> List of x_i values</param>
        /// <param name="k">Security parameter as specified in the setup.</param>
        /// <param name="keyLength">The size of the RSA key in bits</param>
        internal static void GetW(RsaKeyParameters pubKey, byte[] psBytes, BigInteger[] xValues, int k, int keyLength, out BigInteger w)
        {
            var BigK = xValues.Length;

            // ASN.1 encoding of the PublicKey
            var keyBytes = pubKey.ToBytes();

            // Computing ExLen
            var ExLen = Utils.GetByteLength(keyLength);

            // Encoding the x Values
            byte[] ExComb = new byte[0]; // Empty Array (Initialization)
            for (int i = 0; i < BigK; i++)
            {
                // Encoding x_i to an OctetString.
                var tmp = Utils.I2OSP(xValues[i], ExLen);
                ExComb = Utils.Combine(ExComb, tmp);
            }

            // Concatenating the rest of s
            var s = Utils.Combine(keyBytes, psBytes, ExComb);
            // Hash the OctetString
            var BigW = Utils.SHA256(s);
            // Truncate to k-bits
            BigW = Utils.TruncateToKbits(BigW, k);
            // Convert to an Integer and return
            w = Utils.OS2IP(BigW);
        }

        /// <summary>
        /// Calculate the value of K as specified in equation (7) of the setup.
        /// </summary>
        /// <param name="k">Security parameter specified in the setup</param>
        internal static void GetK(int k, out int BigK)
        {
            BigK = k + 1;
            return;
        }

        /// <summary>
        /// Generate r as specified in step 3 of the proving algorithm.
        /// </summary>
        /// <param name="keyLength">The size of the RSA key in bits</param>
        internal static void GetR(int keyLength, out BigInteger r)
        {
            // Initialize a cryptographic randomness.
            SecureRandom random = new SecureRandom();

            // bitSize for the random value r (|N| - 1).
            int bitSize = keyLength - 1;

            // Generate random number that is bitSize long.
            r = new BigInteger(bitSize, random);

            return;
        }

    }

}