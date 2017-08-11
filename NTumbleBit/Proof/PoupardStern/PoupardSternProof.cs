using System;
using NTumbleBit.BouncyCastle.Math;

namespace TumbleBitSetup
{
    public class PoupardSternProof
    {
        internal PoupardSternProof(Tuple<BigInteger[], BigInteger> proof)
        {
            if(proof == null)
                throw new ArgumentNullException(nameof(proof));
            XValues = proof.Item1;
            YValue = proof.Item2;
        }
        internal BigInteger[] XValues
        {
            get; set;
        }
        internal BigInteger YValue
        {
            get; set;
        }
    }
}