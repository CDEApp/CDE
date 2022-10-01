using System;
using System.ComponentModel;
using FlatSharp.Attributes;

namespace cdeLib.Entities;

[Flags]
[FlatBufferEnum(typeof(byte))]
public enum Flags : byte
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