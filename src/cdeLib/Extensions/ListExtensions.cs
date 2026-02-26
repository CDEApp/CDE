using System;
using System.Collections.Generic;

namespace cdeLib.Extensions;

public static class ListExtensions
{
    public static void Sort<T>(this IList<T> list, Comparison<T> comparison)
    {
        if (list is List<T> list1)
        {
            list1.Sort(comparison);
        }
        else
        {
            var copy = new List<T>(list);
            copy.Sort(comparison);
            Copy(copy, 0, list, 0, list.Count);
        }
    }

    private static void Copy<T>(IList<T> sourceList, int sourceIndex,
        IList<T> destinationList, int destinationIndex, int count)
    {
        for (int i = 0; i < count; i++)
        {
            destinationList[destinationIndex + i] = sourceList[sourceIndex + i];
        }
    }
}