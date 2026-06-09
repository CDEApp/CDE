using System;

namespace cdeWin;

/// <summary>
/// see http://stackoverflow.com/questions/98033/wrap-a-delegate-in-an-iequalitycomparer
/// </summary>
public class StrictKeyEqualityComparer<T, TKey>
    : KeyEqualityComparer<T, TKey> where TKey : IEquatable<TKey>
{
    public StrictKeyEqualityComparer(Func<T, TKey> keyExtractor) : base(keyExtractor)
    { }

    public override bool Equals(T x, T y)
    {
        // This will use the overload that accepts a TKey parameter
        // instead of an object parameter.
        return KeyExtractor(x).Equals(KeyExtractor(y));
    }
}