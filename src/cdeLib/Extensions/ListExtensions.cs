using System;
using System.Collections;
using System.Collections.Generic;

namespace cdeLib.Extensions;

public static class ListExtensions
{
    public static void TruncateList(this IList iList, int max)
    {
        for (var i = iList.Count - 1; i >= max; i--)
        {
            iList.RemoveAt(i);
        }
    }

    /// <summary>
    /// Sorts an IList using the specified comparison. Optimized for List&lt;T&gt; and T[] fast paths.
    /// </summary>
    public static void Sort<T>(this IList<T> list, Comparison<T> comparison)
    {
        switch (list)
        {
            // Fast path: List<T> has optimized Sort implementation
            case List<T> concreteList:
                concreteList.Sort(comparison);
                return;
            // Fast path: Array has optimized Sort implementation
            case T[] array:
                Array.Sort(array, comparison);
                return;
            default:
                // Slow path: Generic IList<T> - must copy, sort, and copy back
                SortGenericList(list, comparison);
                break;
        }
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

        // Copy back from the array
        for (int i = 0; i < count; i++)
        {
            list[i] = tempArray[i];
        }
    }
}