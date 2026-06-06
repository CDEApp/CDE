using System;
using System.Collections.Generic;
using System.IO;

namespace cdeLib.Entities.Soa;

/// <summary>
/// Lightweight adapter presenting a single <see cref="EntryStore"/> entry (by index) as an
/// <see cref="ICommonEntry"/>, so existing tree-oriented consumers (GUI display, navigation,
/// dupes read paths) can run on the struct-of-arrays model without materialising the whole
/// pointer tree. Read members map onto the store's arrays; build/mutate members throw, since a
/// store is produced wholesale by the loader, not edited entry-by-entry.
///
/// Intended for OCCASIONAL access (displaying a directory, a search result row). Bulk traversal
/// should use index-based APIs (<see cref="EntryStoreSearch"/>) to avoid per-entry wrapper allocs.
/// </summary>
public sealed class EntryRef : ICommonEntry
{
    private readonly EntryStore _store;
    private readonly int _index;

    public EntryRef(EntryStore store, int index)
    {
        _store = store;
        _index = index;
    }

    public EntryStore Store => _store;
    public int Index => _index;

    private static NotSupportedException ReadOnly([System.Runtime.CompilerServices.CallerMemberName] string m = null)
        => new($"EntryRef is a read-only view over EntryStore; '{m}' is not supported.");

    public string Path { get => _store.FullName(_index); set => throw ReadOnly(); }
    public long Size { get => _store.Size[_index]; set => throw ReadOnly(); }
    public DateTime Modified { get => _store.Modified(_index); set => throw ReadOnly(); }

    public bool IsDirectory { get => _store.IsDirectory(_index); set => throw ReadOnly(); }
    public bool IsHashDone { get => _store.IsHashDone(_index); set => throw ReadOnly(); }
    public bool IsPartialHash { get => _store.IsPartialHash(_index); set => throw ReadOnly(); }
    public bool IsModifiedBad
    {
        get => (_store.Flags(_index) & Flags.ModifiedBad) == Flags.ModifiedBad;
        set => throw ReadOnly();
    }
    public bool IsReparsePoint
    {
        get => (_store.Flags(_index) & Flags.ReparsePoint) == Flags.ReparsePoint;
        set => throw ReadOnly();
    }
    public bool IsDefaultSort { get => true; set => throw ReadOnly(); } // store is built in sorted order

    public Hash16 Hash
    {
        get => _store.Hash != null ? _store.Hash[_index] : default;
        set => throw ReadOnly();
    }

    public string FullPath => _store.FullPath(_index);

    public bool PathProblem
    {
        get
        {
            for (var cur = _index; cur != EntryStore.None; cur = _store.Parent[cur])
            {
                var name = _store.Name[cur];
                if (!string.IsNullOrEmpty(name) && (name.EndsWith(' ') || name.EndsWith('.'))) return true;
            }
            return false;
        }
    }

    public IReadOnlyList<ICommonEntry> Children
    {
        get
        {
            // Gate on having children, not on the directory flag: the root is not flagged a
            // directory yet has children (matching RootEntry), and a file simply has none.
            if (_store.FirstChild[_index] == EntryStore.None) return null;
            List<ICommonEntry> list = null;
            foreach (var c in _store.Children(_index))
            {
                (list ??= new List<ICommonEntry>()).Add(new EntryRef(_store, c));
            }
            return list;
        }
    }

    public ICommonEntry ParentCommonEntry
    {
        get
        {
            var p = _store.Parent[_index];
            return p == EntryStore.None ? null : new EntryRef(_store, p);
        }
        set => throw ReadOnly();
    }

    public uint FileEntryCount { get => CountSubtree().Files; set => throw ReadOnly(); }
    public uint DirEntryCount { get => CountSubtree().Dirs; set => throw ReadOnly(); }

    private (uint Files, uint Dirs) CountSubtree()
    {
        uint files = 0, dirs = 0;
        var stack = new Stack<int>();
        stack.Push(_index);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            foreach (var c in _store.Children(n))
            {
                if (_store.IsDirectory(c)) { dirs++; stack.Push(c); }
                else files++;
            }
        }
        return (files, dirs);
    }

    public int PathCompareWithDirTo(ICommonEntry de)
    {
        if (de == null) return -1;
        if (IsDirectory && !de.IsDirectory) return -1;
        if (!IsDirectory && de.IsDirectory) return 1;
        return string.Compare(Path, de.Path, StringComparison.OrdinalIgnoreCase);
    }

    public int SizeCompareWithDirTo(ICommonEntry de)
    {
        if (de == null) return -1;
        if (IsDirectory && !de.IsDirectory) return -1;
        if (!IsDirectory && de.IsDirectory) return 1;
        var c = Size.CompareTo(de.Size);
        return c != 0 ? c : string.Compare(Path, de.Path, StringComparison.OrdinalIgnoreCase);
    }

    public int ModifiedCompareTo(ICommonEntry de)
    {
        if (de == null) return -1;
        if (IsModifiedBad && !de.IsModifiedBad) return -1;
        if (!IsModifiedBad && de.IsModifiedBad) return 1;
        if (IsModifiedBad && de.IsModifiedBad) return 0;
        return DateTime.Compare(Modified, de.Modified);
    }

    public string MakeFullPath(ICommonEntry dirEntry)
    {
        var parent = FullPath;
        var name = dirEntry?.Path ?? "dnull";
        if (parent.Length > 0 && parent[^1] != '\\' && parent[^1] != '/')
            return string.Concat(parent, System.IO.Path.DirectorySeparatorChar.ToString(), name);
        return string.Concat(parent, name);
    }

    public IList<ICommonEntry> GetListFromRoot()
    {
        var list = new List<ICommonEntry>(8);
        for (var cur = _index; cur != EntryStore.None; cur = _store.Parent[cur])
        {
            list.Add(new EntryRef(_store, cur));
        }
        list.Reverse();
        return list;
    }

    public bool ExistsOnFileSystem() => Directory.Exists(FullPath);

    public void TraverseTreePair(TraverseFunc func)
    {
        if (func == null) return;
        var stack = new Stack<int>();
        stack.Push(_index);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            var parentRef = new EntryRef(_store, n);
            foreach (var c in _store.Children(n))
            {
                if (!func(parentRef, new EntryRef(_store, c))) return;
                if (_store.IsDirectory(c)) stack.Push(c);
            }
        }
    }

    // ----- build / mutate members: not supported on a read-only store view -----
    public void AddChild(DirEntry child) => throw ReadOnly();
    public void SetSummaryFields() => throw ReadOnly();
    public void SetHash(byte[] hashResponseHash) => throw ReadOnly();
    public void TraverseTreesCopyHash(ICommonEntry destination) => throw ReadOnly();
}
