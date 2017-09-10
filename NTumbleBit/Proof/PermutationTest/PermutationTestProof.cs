using System;

namespace TumbleBitSetup
{
    public class PermutationTestProof
    {
        public PermutationTestProof(byte[][] proof)
        {
            if(proof == null)
                throw new ArgumentNullException(nameof(proof));
            Signatures = proof;
        }

        public byte[][] Signatures
        {
            get; set;
        }
    }
}
