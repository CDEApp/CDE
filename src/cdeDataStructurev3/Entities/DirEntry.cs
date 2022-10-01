using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using ProtoBuf;

namespace cdeDataStructure3.Entities;

[DebuggerDisplay(
    "Path = {Path} {Size}, Count = {Children != null ? Children.Count : 0} P{IsPartialHash} #{Hash.HashB}")]
[ProtoContract]
public class DirEntry : CommonEntry
{
    [Flags]
    public enum Flags
    {
        [Description("Obligatory none value.")]
        None = 0,

        [Description("Is a directory.")]
        Directory = 1 << 0,

        [Description("Has a bad modified date field.")]
        ModifiedBad = 1 << 1,

        // [Obsolete("With dotnetcore3.0")]
        // [Description("Is a symbolic link.")]
        // SymbolicLink = 1 << 2,
        [Description("Is a reparse point.")]
        ReparsePoint = 1 << 3,

        [Description("Hashing was done for this.")]
        HashDone = 1 << 4,

        [Description("The Hash if done was a partial.")]
        PartialHash = 1 << 5,

        [Description("The Children are allready in default order.")]
        DefaultSort = 1 << 6
    };

    [ProtoMember(1, IsRequired = true)]
    public DateTime Modified { get; set; }

    [ProtoMember(2, IsRequired = false)]
    public Hash16 Hash;

    [ProtoMember(5, IsRequired = false)] // is there a better default value than 0 here
    public Flags BitFields;

    #region BitFields based properties

    public bool IsDirectory
    {
        get => (BitFields & Flags.Directory) == Flags.Directory;
        set
        {
            if (value)
            {
                BitFields |= Flags.Directory;
            }
            else
            {
                BitFields &= ~Flags.Directory;
            }
        }
    }

    public bool IsModifiedBad
    {
        get => (BitFields & Flags.ModifiedBad) == Flags.ModifiedBad;
        set
        {
            if (value)
            {
                BitFields |= Flags.ModifiedBad;
            }
            else
            {
                BitFields &= ~Flags.ModifiedBad;
            }
        }
    }

    public bool IsReparsePoint
    {
        get => (BitFields & Flags.ReparsePoint) == Flags.ReparsePoint;
        set
        {
            if (value)
            {
                BitFields |= Flags.ReparsePoint;
            }
            else
            {
                BitFields &= ~Flags.ReparsePoint;
            }
        }
    }

    public bool IsHashDone
    {
        get => (BitFields & Flags.HashDone) == Flags.HashDone;
        set
        {
            if (value)
            {
                BitFields |= Flags.HashDone;
            }
            else
            {
                BitFields &= ~Flags.HashDone;
            }
        }
    }

    public bool IsPartialHash
    {
        get => (BitFields & Flags.PartialHash) == Flags.PartialHash;
        set
        {
            if (value)
            {
                BitFields |= Flags.PartialHash;
            }
            else
            {
                BitFields &= ~Flags.PartialHash;
            }
        }
    }

    public bool IsDefaultSort
    {
        get => (BitFields & Flags.DefaultSort) == Flags.DefaultSort;
        set
        {
            if (value)
            {
                BitFields |= Flags.DefaultSort;
            }
            else
            {
                BitFields &= ~Flags.DefaultSort;
            }
        }
    }

    #endregion

    /// <summary>
    /// if this is a directory number of files contained in its hierarchy
    /// </summary>
    public long FileEntryCount;

    /// <summary>
    /// if this is a directory number of dirs contained in its hierarchy
    /// </summary>
    public long DirEntryCount;

    public DirEntry()
    {
    }

    /// <summary>
    /// For Testing.
    /// </summary>
    public DirEntry(bool isDirectory)
    {
        IsDirectory = isDirectory;
        if (isDirectory)
        {
            Children = new List<DirEntry>();
        }
    }

    public DirEntry(FileSystemInfo fs) : this()
    {
        Path = fs.Name;
        Modified = fs.LastWriteTime;
        IsDirectory = (fs.Attributes & FileAttributes.Directory) != 0;
        IsReparsePoint = (fs.Attributes & FileAttributes.ReparsePoint) != 0;
        if (fs is FileInfo info)
        {
            Size = info.Length;
        }
        else
        {
            Children = new List<DirEntry>();
        }
    }

    public const CompareOptions MyCompareOptions = CompareOptions.IgnoreCase | CompareOptions.StringSort;
    public static readonly CompareInfo MyCompareInfo = CompareInfo.GetCompareInfo("en-US");

    // is this right ? for the simple compareResult invert we do in caller ? - maybe not ? keep dirs at top anyway ?
    public int PathCompareWithDirTo(DirEntry de)
    {
        if (de == null)
        {
            return -1; // this before de
        }

        if (IsDirectory && !de.IsDirectory)
        {
            return -1; // this before de
        }

        if (!IsDirectory && de.IsDirectory)
        {
            return 1; // this after de
        }

        return MyCompareInfo.Compare(Path, de.Path, MyCompareOptions);
    }

    // can this be done with TraverseTree ?
    public void SetSummaryFields()
    {
        var size = 0L;
        var dirEntryCount = 0L;
        var fileEntryCount = 0L;
        PathProblem = IsBadPath();

        if (IsDirectory && Children != null)
        {
            var childrenDirEntryCount = 0L;
            foreach (var dirEntry in Children)
            {
                if (dirEntry.IsDirectory)
                {
                    dirEntry.SetSummaryFields();
                    if (PathProblem) // infects child entries
                    {
                        dirEntry.PathProblem = PathProblem;
                    }

                    ++dirEntryCount;
                }
                else
                {
                    dirEntry.PathProblem = dirEntry.IsBadPath();
                }

                size += dirEntry.Size;
                fileEntryCount += dirEntry.FileEntryCount;
                childrenDirEntryCount += dirEntry.DirEntryCount;
            }

            fileEntryCount += Children.Count - dirEntryCount;
            dirEntryCount += childrenDirEntryCount;
        }

        FileEntryCount = fileEntryCount;
        DirEntryCount = dirEntryCount;
        Size = size;
    }
}