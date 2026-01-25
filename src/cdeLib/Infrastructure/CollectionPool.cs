using System.Collections.Generic;
using cdeLib.Entities;

namespace cdeLib.Infrastructure;

/// <summary>
/// Specialized pools for frequently used collection types
/// </summary>
public static class CollectionPool
{
    private static readonly ObjectPool<List<PairDirEntry>> PairDirEntryListPool =
        new(() => new List<PairDirEntry>(100), list => list.Clear(), 20);

    private static readonly ObjectPool<List<string>> StringListPool =
        new(() => new List<string>(50), list => list.Clear(), 30);

    private static readonly ObjectPool<Stack<ICommonEntry>> CommonEntryStackPool =
        new(() => new Stack<ICommonEntry>(), stack => stack.Clear(), 15);

    private static readonly ObjectPool<Dictionary<string, object>> StringDictionaryPool =
        new(() => new Dictionary<string, object>(), dict => dict.Clear(), 25);

    private static readonly ObjectPool<List<DirEntry>> DirEntryListPool =
        new(() => new List<DirEntry>(4), list => list.Clear());

    // PairDirEntry List Pool
    public static List<PairDirEntry> GetPairDirEntryList() => PairDirEntryListPool.Get();
    public static void ReturnPairDirEntryList(List<PairDirEntry> list) => PairDirEntryListPool.Return(list);

    // String List Pool  
    public static List<string> GetStringList() => StringListPool.Get();
    public static void ReturnStringList(List<string> list) => StringListPool.Return(list);

    // Common Entry Stack Pool
    public static Stack<ICommonEntry> GetCommonEntryStack() => CommonEntryStackPool.Get();
    public static void ReturnCommonEntryStack(Stack<ICommonEntry> stack) => CommonEntryStackPool.Return(stack);

    // String Dictionary Pool
    public static Dictionary<string, object> GetStringDictionary() => StringDictionaryPool.Get();
    public static void ReturnStringDictionary(Dictionary<string, object> dict) => StringDictionaryPool.Return(dict);

    // DirEntry List Pool
    public static List<DirEntry> GetDirEntryList() => DirEntryListPool.Get();
    public static void ReturnDirEntryList(List<DirEntry> list) => DirEntryListPool.Return(list);

    /// <summary>
    /// Clear all pools - typically called during application shutdown
    /// </summary>
    public static void ClearAll()
    {
        PairDirEntryListPool.Clear();
        StringListPool.Clear();
        CommonEntryStackPool.Clear();
        StringDictionaryPool.Clear();
        DirEntryListPool.Clear();
    }
}