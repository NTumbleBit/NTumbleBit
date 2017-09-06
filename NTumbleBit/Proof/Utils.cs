using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using NTumbleBit.BouncyCastle.Crypto.Digests;
using NTumbleBit.BouncyCastle.Crypto.Generators;
using NTumbleBit.BouncyCastle.Crypto.Parameters;
using NTumbleBit.BouncyCastle.Security;
using NTumbleBit.BouncyCastle.Crypto;
using NTumbleBit.BouncyCastle.Math;

namespace TumbleBitSetup
{
    public class Utils
    {
        /// <summary>
        /// MGF1 Mask Generation Function based on the SHA-256 hash function
        /// </summary>
        /// <param name="data">Input to process</param>
        /// <param name="keySize">The size of the RSA key in bits</param>
        /// <returns>Hashed result as a 256 Bytes array (2048 Bits)</returns>
        internal static byte[] MGF1_SHA256(byte[] data, int keySize)
        {
            byte[] output = new byte[GetByteLength(keySize)];
            Sha256Digest sha256 = new Sha256Digest();
            var generator = new Mgf1BytesGenerator(sha256);
            generator.Init(new MgfParameters(data));
            generator.GenerateBytes(output, 0, output.Length);
            return output;
        }

        /// <summary>
        /// Combines two or more byteArrays
        /// </summary>
        /// <param name="arrays">List of arrays to combine</param>
        /// <returns>The resultant combined list</returns>
        internal static byte[] Combine(params byte[][] arrays)
        {
            // From NTumbleBit https://github.com/NTumbleBit/NTumbleBit/blob/master/NTumbleBit/Utils.cs#L61
            var len = arrays.Select(a => a.Length).Sum();
            int offset = 0;
            var combined = new byte[len];
            foreach (var array in arrays)
            {
                Array.Copy(array, 0, combined, offset, array.Length);
                offset += array.Length;
            }
            return combined;
        }

        /// <summary>
        /// Returns how many Octets are needed to represent the integer x
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        internal static int GetOctetLen(int x)
        {
            return (int)Math.Ceiling((1.0 / 8.0) * Math.Log(x+1, 2));
        }

        internal static AsymmetricCipherKeyPair GeneratePrivate(BigInteger exp, int keySize)
        {
            SecureRandom random = new SecureRandom();
            var gen = new RsaKeyPairGenerator();
            gen.Init(new RsaKeyGenerationParameters(exp, random, keySize, 2)); // See A.15.2 IEEE P1363 v2 D1 for certainty parameter
            return gen.GenerateKeyPair();
        }

        /// <summary>
        /// Generates a private key given P, Q and e
        /// </summary>
        /// <param name="p">P</param>
        /// <param name="q">Q</param>
        /// <param name="e">Public Exponent</param>
        /// <returns>RSA key pair</returns>
        internal static AsymmetricCipherKeyPair GeneratePrivate(BigInteger p, BigInteger q, BigInteger e)
        {
            BigInteger n = p.Multiply(q);

            BigInteger One = BigInteger.One;
            BigInteger pSub1 = p.Subtract(One);
            BigInteger qSub1 = q.Subtract(One);
            BigInteger gcd = pSub1.Gcd(qSub1);
            BigInteger lcm = pSub1.Divide(gcd).Multiply(qSub1);

            //
            // calculate the private exponent
            //
            BigInteger d = e.ModInverse(lcm);

            if(d.BitLength <= q.BitLength)
                throw new ArgumentException("Invalid RSA q value");

            //
            // calculate the CRT factors
            //
            BigInteger dP = d.Remainder(pSub1);
            BigInteger dQ = d.Remainder(qSub1);
            BigInteger qInv = q.ModInverse(p);

            return new AsymmetricCipherKeyPair(
                new RsaKeyParameters(false, n, e),
                new RsaPrivateCrtKeyParameters(n, e, d, p, q, dP, dQ, qInv));
        }

        /// <summary>
        /// Generates a list of primes up to and including the input bound
        /// </summary>
        /// <param name="bound"> Bound to generate primes up to</param>
        /// <returns> Iterator over the list of primes</returns>
        internal static IEnumerable<int> Primes(int bound)
        {
            // From here https://codereview.stackexchange.com/questions/56480/getting-all-primes-between-0-n

            if (bound < 2) yield break;
            //The first prime number is 2
            yield return 2;

            BitArray composite = new BitArray((bound - 1) / 2);
            int limit = ((int)(Math.Sqrt(bound)) - 1) / 2;
            for (int i = 0; i < limit; i++)
            {
                if (composite[i]) continue;
                //The first number not crossed out is prime
                int prime = 2 * i + 3;
                yield return prime;
                //cross out all multiples of this prime, starting at the prime squared
                for (int j = (prime * prime - 2) >> 1; j < composite.Length; j += prime)
                {
                    composite[j] = true;
                }
            }
            //The remaining numbers not crossed out are also prime
            for (int i = limit; i < composite.Length; i++)
            {
                if (!composite[i]) yield return 2 * i + 3;
            }
        }

        /// <summary>
        /// converts a non-negative integer to an octet string of a specified length.
        /// </summary>
        /// <param name="x">non-negative integer</param>
        /// <param name="xLen">specified length</param>
        /// <returns></returns> 
        internal static byte[] I2OSP(int x, int xLen)
        {
            if (x < 0)
                throw new ArgumentOutOfRangeException("only positive integers");

            // checks If x >= 256^xLen
            if (BigInteger.ValueOf(x).CompareTo(BigInteger.ValueOf(256).Pow(xLen)) >= 0)
                throw new ArithmeticException("integer too large");

            byte[] outBytes = new byte[xLen];

            // converts x to an unsigned byteArray.
            for (int i = 0; (x > 0) && (i < outBytes.Length); i++)
            {
                outBytes[i] = (byte)(x % 256);
                x /= 256;
            }
            
            // make sure the output is BigEndian
            if (BitConverter.IsLittleEndian)
                Array.Reverse(outBytes, 0, outBytes.Length);

            return outBytes;
        }

        /// <summary>
        /// converts a non-negative BigInteger to an octet string of a specified length.
        /// </summary>
        /// <param name="x">non-negative BigInteger</param>
        /// <param name="xLen">specified length</param>
        /// <returns></returns> 
        internal static byte[] I2OSP(BigInteger x, int xLen)
        {
            var N256 = BigInteger.ValueOf(256);

            if (x.CompareTo(BigInteger.Zero) < 0)
                throw new ArgumentOutOfRangeException("only positive integers");

            // checks If x >= 256^xLen
            if (x.CompareTo(N256.Pow(xLen)) >= 0)
                throw new ArithmeticException("integer too large");

            byte[] outBytes = new byte[xLen];

            // converts x to an unsigned byteArray.
            for (int i = 0; (x.CompareTo(BigInteger.Zero) > 0) && (i < outBytes.Length); i++)
            {
                outBytes[i] = (byte)(x.Mod(N256).LongValue);
                x = x.Divide(N256);
            }

            // make sure the output is BigEndian
            if (BitConverter.IsLittleEndian)
                Array.Reverse(outBytes, 0, outBytes.Length);

            return outBytes;
        }

        /// <summary>
        /// converts an octet string to a nonnegative BigInteger.
        /// </summary>
        /// <param name="x">Octet String</param>
        /// <returns></returns>
        internal static BigInteger OS2IP(byte[] x)
        {
            int i;

            // To skip the first leading zeros (if they exist)
            for (i = 0; (i < x.Length) && (x[i] == 0x00); i++)
                continue;
            i--;

            if (i > 0)
            {
                // If there exits leading zeros, skip them
                byte[] result = new byte[x.Length - i];
                Buffer.BlockCopy(x, i, result, 0, result.Length);
                return new BigInteger(1, result);
            }
            else
                return new BigInteger(1, x);
        }

        internal static byte[] SHA256(byte[] data)
        {
            return SHA256(data, 0, data.Length);
        }
        
        /// <summary>
        /// A SHA256 hashing oracle (H_2 in the setup)
        /// </summary>
        /// <param name="data">message to be hashed</param>
        /// <param name="offset">offset in the input message</param>
        /// <param name="count">Amount of bytes to be hashed in the message</param>
        /// <returns></returns>
        internal static byte[] SHA256(byte[] data, int offset, int count)
        {
			Sha256Digest sha256 = new Sha256Digest();
			sha256.BlockUpdate(data, offset, count);
			byte[] rv = new byte[32];
			sha256.DoFinal(rv, 0);
			return rv;
        }

        /// <summary>
        /// Truncates k-bits from the source byteArray.
        /// </summary>
        /// <param name="srcArray"></param>
        /// <param name="k"> Amount of bits to truncate</param>
        /// <returns>byteArray with k-bits</returns>
        internal static byte[] TruncateToKbits(byte[] srcArray, int k)
        {
            // Number of bytes needed to represent k bits
            int nBytes = GetByteLength(k);

            // Initialize an output array 
            byte[] dstArray = new byte[nBytes];

            // Fill dstArray with the first nBytes of srcArray
            System.Buffer.BlockCopy(srcArray, 0, dstArray, 0, nBytes);

            return dstArray;
        }

        /// <summary>
        /// Returns the amount of bytes needed to represent the given bits
        /// </summary>
        /// <param name="nBits">Number of Bits</param>
        /// <returns></returns>
        internal static int GetByteLength(int nBits)
        {
            if (nBits < 0)
                throw new ArgumentOutOfRangeException("Invalid number of bits");
            int BitsPerByte = 8;
            return (nBits + BitsPerByte - 1) / BitsPerByte;
        }
    }
}
