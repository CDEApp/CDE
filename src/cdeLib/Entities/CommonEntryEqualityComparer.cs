using System.Collections.Generic;

namespace cdeLib.Entities;

public class CommonEntryEqualityComparer : IEqualityComparer<ICommonEntry>
{
    public bool Equals(ICommonEntry x, ICommonEntry y)
    {
        return StaticEquals(x, y);
    }

    public int GetHashCode(ICommonEntry obj)
    {
        return StaticGetHashCode(obj);
    }

    private static bool StaticEquals(ICommonEntry x, ICommonEntry y)
    {
        if (x == null || y == null
                      || !x.IsHashDone || !y.IsHashDone)
        {
            return false;
        }

        return Hash16.EqualityComparer.StaticEquals(x.Hash, y.Hash)
               && x.Size == y.Size;
    }

    private static int StaticGetHashCode(ICommonEntry obj)
    {
        // quite likely a bad choice for hash.
        // if Hash not set then. avoid using it...
        if (obj.IsHashDone)
        {
            return (Hash16.EqualityComparer.StaticGetHashCode(obj.Hash) * 31 +
                    (int)(obj.Size >> 32)) * 31 +
                   (int)(obj.Size & 0xFFFFFFFF);
        }

        return (int)(obj.Size >> 32) * 31 +
               (int)(obj.Size & 0xFFFFFFFF);
    }
}