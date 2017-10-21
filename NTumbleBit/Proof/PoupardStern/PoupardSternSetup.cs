using System;

namespace TumbleBitSetup
{
    public class PoupardSternSetup
    {
        public PoupardSternSetup()
        {

        }
        public PoupardSternSetup(byte[] publicString, int keySize, int securityParameter = 128)
        {
            if (KeySize < 0)
                throw new ArgumentOutOfRangeException(nameof(keySize));
            SecurityParameter = securityParameter;
            PublicString = publicString ?? throw new ArgumentNullException(nameof(publicString));
            KeySize = keySize;
        }
        public byte[] PublicString
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

        public PoupardSternSetup Clone()
        {
            return new PoupardSternSetup()
            {
                KeySize = KeySize,
                SecurityParameter = SecurityParameter,
                PublicString = PublicString
            };
        }
    }
}
