using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace cdeWin;

public static class EnumerableStringExtensions
{
    public static AutoCompleteStringCollection ToAutoCompleteStringCollection(this IEnumerable<string> enumerable)
    {
        if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
        var autoComplete = new AutoCompleteStringCollection();
        foreach (var item in enumerable)
        {
            autoComplete.Add(item);
        }
        return autoComplete;
    }
}