using System;

namespace TumbleBitSetup
{
    public class PermutationTestProof
    {
        public PermutationTestProof(byte[][] proof)
        {
            Signatures = proof ?? throw new ArgumentNullException(nameof(proof));
        }

        public byte[][] Signatures
        {
            get; set;
        }
    }
}
