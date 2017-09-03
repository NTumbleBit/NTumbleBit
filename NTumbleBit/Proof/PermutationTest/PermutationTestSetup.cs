using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TumbleBitSetup
{
    public class PermutationTestSetup
    {
        public PermutationTestSetup()
        {

        }

        public PermutationTestSetup(byte[] publicString, int alpha, int keySize, int securityParameter = 128)
        {
            if(publicString == null)
                throw new ArgumentNullException(nameof(publicString));
            if(KeySize < 0)
                throw new ArgumentOutOfRangeException(nameof(keySize));
            Alpha = alpha;
            PublicString = publicString;
            KeySize = keySize;
            SecurityParameter = securityParameter;
        }
        public byte[] PublicString
        {
            get; set;
        }
        public int Alpha
        {
            get; set;
        }
        public int SecurityParameter
        {
            get; set;
        } = 128;

        public int KeySize
        {
            get; set;
        }

        public PermutationTestSetup Clone()
        {
            return new PermutationTestSetup()
            {
                KeySize = KeySize,
                Alpha = Alpha,
                SecurityParameter = SecurityParameter,
                PublicString = PublicString
            };
        }
    }
}
