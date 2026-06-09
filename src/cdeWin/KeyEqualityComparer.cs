using System;
using System.Collections.Generic;

namespace cdeWin;

/// <summary>
/// see http://stackoverflow.com/questions/98033/wrap-a-delegate-in-an-iequalitycomparer
/// </summary>
public class KeyEqualityComparer<T, TKey> : IEqualityComparer<T>
{
    protected readonly Func<T, TKey> KeyExtractor;

    protected KeyEqualityComparer(Func<T, TKey> keyExtractor)
    {
        KeyExtractor = keyExtractor;
    }

    public virtual bool Equals(T x, T y)
    {
        return KeyExtractor(x).Equals(KeyExtractor(y));
    }

    public int GetHashCode(T obj)
    {
        return KeyExtractor(obj).GetHashCode();
    }
}