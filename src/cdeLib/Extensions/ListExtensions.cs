using System;
using System.Collections.Generic;

namespace cdeLib.Extensions;

public static class ListExtensions
{
    /// <summary>
    /// Sorts an IList using the specified comparison. Optimized for List&lt;T&gt; and T[] fast paths.
    /// </summary>
    public static void Sort<T>(this IList<T> list, Comparison<T> comparison)
    {
        // Fast path: List<T> has optimized Sort implementation
        if (list is List<T> concreteList)
        {
            concreteList.Sort(comparison);
            return;
        }

        // Fast path: Array has optimized Sort implementation
        if (list is T[] array)
        {
            Array.Sort(array, comparison);
            return;
        }

        // Slow path: Generic IList<T> - must copy, sort, and copy back
        SortGenericList(list, comparison);
    }

    /// <summary>
    /// Sorts a generic IList by creating a temporary array, sorting it, and copying back.
    /// </summary>
    private static void SortGenericList<T>(IList<T> list, Comparison<T> comparison)
    {
        var count = list.Count;

        // Use array instead of List for slightly better performance
        var tempArray = new T[count];

        // Copy to array
        for (int i = 0; i < count; i++)
        {
            tempArray[i] = list[i];
        }

        // Sort using optimized array sort
        Array.Sort(tempArray, comparison);

        // Copy back from array
        for (int i = 0; i < count; i++)
        {
            list[i] = tempArray[i];
        }
    }
}