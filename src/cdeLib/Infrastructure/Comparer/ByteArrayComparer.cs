using System;
using System.Collections.Generic;
using System.Linq;

namespace cdeLib.Infrastructure.Comparer
{
    public class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        #region IEqualityComparer<byte[]> Members

        public bool Equals(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }
            return left.SequenceEqual(right);
        }

        public int GetHashCode(byte[] key)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            return key.Aggregate(17, (current, b) => current * 31 + b); //ref: http://stackoverflow.com/questions/3613102/why-use-a-prime-number-in-hashcode
        }

        #endregion
    }
}