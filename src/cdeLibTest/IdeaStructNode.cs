using System;
using System.Collections.Generic;
using System.ComponentModel;
using cdeLib.Entities;
using cdeLib.Infrastructure;

#pragma warning disable 0649
namespace cdeLibTest;
// limit 2 billion entries.
// limit 2 billion unique entry names

[Flags]
public enum Flags
{
    [Description("Obligatory none value.")]
    None = 0,
    [Description("Is a directory.")]
    Directory = 1 << 0,
    [Description("Has a bad modified date field.")]
    ModifiedBad = 1 << 1,
    [Description("Is a symbolic link.")]
    SymbolicLink = 1 << 2,
    [Description("Is a reparse point.")]
    ReparsePoint = 1 << 3,
    [Description("Hashing was done for this.")]
    HashDone = 1 << 4,
    [Description("The Hash if done was a partial.")]
    PartialHash = 1 << 5,
    [Description("The Children are allready in default order.")]
    DefaultSort = 1 << 6
}

internal class MyCatalogClass
{
    // if searching only by entry name, then just step through array building list, then render after ?
    public List<Root> Roots;

    public MyCatalogClass()
    {
        Roots = new List<Root>(10);
    }
}

internal struct ArrayThing<T>
{
    public T[] Array;  // to address max array size... list of blocks ? like did in EntryStore ? icky icky icky

    public int NextFree;
}

internal struct Root
{
    public int NextFreeEntryName;
    public string[] EntryName; // avoid 0
    public int NextFreePath;
    public string[] Paths; // for assembled parent paths of things ?

    public int NextFreeNode;
    public Node[] Nodes; // avoid 0
    //  
    // might be useful to order Nodes in an breadth first heirarchy order
    // then path can be modified up/down relatively painlessly using stringbuffer as go ?
    // dont care about depth first ? for our use case....
    // if breadth first then carrying along path isnt hard ? for compares in builder ?
    // - in normal breadth first RecurseTree getting it breadth first is easy.
    //
    //
    //
    //
    // keep a int[] sorted by modifiedDate refernce to all blocks.
    //   this then could be binary searched for date/time limits ? [not commmon mind you]
    // similar for size field maybe ?
    //

    public long[] FilesCount; // same index as Nodes
    public long[] DirsCount; // same index as Nodes
    public long[] PathName; // same index as Nodes

    public Flags[] Flags;  // ? (4)
    public Hash16[] Hashes; // ? ? maybe reorder this and Nodes when looking for dupes ? 

    // root info
    // volumename, description, defaultfilename, driveletterhint, availspace, totalspace, 
    // ScanStartUTC, ScanEndUTC, ActualFileName
}

// maybe make this the hot path ?
internal struct Node
{
    public int FileName; // reference TreeSet EntryName (4)
    public int Sibling; // reference Nodes, sibling of this Node (4)
    public int Child; // reference Nodes, child of this Node (4)
    public int Parent;  // reference Nodes, parent of this Node, rootNode = 0; (4)
    public long Size; // (8)
    public DateTime Modified; // (8)
    public Flags BitFields; // (4)
    public Hash16 Hash; // (16) -- ..(52)
}

internal class IdeaStructNode
{
}
#pragma warning restore 0649