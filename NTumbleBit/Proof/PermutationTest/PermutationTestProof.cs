using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
