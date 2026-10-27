using System;
using System.ComponentModel;

namespace cdeLib.Entities;

[Flags]
public enum Flags : byte
{
    [Description("Obligatory none value.")]
    None = 0,
    [Description("Is a directory.")]
    Directory = 1 << 0,
    [Description("Has a bad modified date field.")]
    ModifiedBad = 1 << 1,
    [Description("Is a reparse point.")]
    ReparsePoint = 1 << 3,
    [Description("Hashing was done for this.")]
    HashDone = 1 << 4,
    [Description("The Hash if done was a partial.")]
    PartialHash = 1 << 5,
    [Description("The Children are allready in default order.")]
    DefaultSort = 1 << 6,
    [Description("A partial hash made by the fixed first-N-bytes algorithm (issue #37). A PartialHash without this bit is a legacy partial hash and is treated as not hashed.")]
    PartialHashV2 = 1 << 7
}

public static class FlagsExtensions
{
    /// <summary>
    /// True when the entry has a hash that may be trusted. A partial hash without <see cref="Flags.PartialHashV2"/>
    /// was made by the pre-#37 algorithm (it hashed the wrong block) and counts as not hashed.
    /// </summary>
    public static bool IsHashUsable(this Flags flags)
    {
        if ((flags & Flags.HashDone) == 0)
        {
            return false;
        }

        return (flags & Flags.PartialHash) == 0 || (flags & Flags.PartialHashV2) != 0;
    }
}