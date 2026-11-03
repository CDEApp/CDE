using System;

namespace cdeLib.Entities.Columnar;

public readonly ref struct EntryFlagsColumn
{
    private readonly ReadOnlySpan<byte> _bits;
    private readonly byte _keep;

    internal EntryFlagsColumn(ReadOnlySpan<byte> bits, byte keep)
    {
        _bits = bits;
        _keep = keep;
    }

    public Flags this[int i] => (Flags)(_bits[i] & _keep);

    public int Length => _bits.Length;
}
