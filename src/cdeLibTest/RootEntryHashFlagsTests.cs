using cdeLib.Entities;
using cdeLib.Infrastructure;
using NUnit.Framework;

namespace cdeLibTest;

[TestFixture]
public class RootEntryHashFlagsTests
{
    [Test]
    public void SetHash_UnsignedValue_MarksHashDone()
    {
        var e = new RootEntry();

        e.SetHash(42UL);

        Assert.That(e.IsHashDone, Is.True);
        Assert.That(e.Hash, Is.EqualTo(new Hash16(42UL, 0)));
    }

    [Test]
    public void IsPartialHash_SetAndClear_TogglesBothPartialBits()
    {
        var e = new RootEntry();
        e.SetHash(1UL);

        e.IsPartialHash = true;
        Assert.That(e.IsPartialHash, Is.True);
        Assert.That(e.BitFields & cdeLib.Entities.Flags.PartialHashV2, Is.EqualTo(cdeLib.Entities.Flags.PartialHashV2));
        Assert.That(e.IsHashDone, Is.True, "a V2 partial hash is usable");

        e.IsPartialHash = false;
        Assert.That(e.IsPartialHash, Is.False);
        Assert.That(e.BitFields & cdeLib.Entities.Flags.PartialHashV2, Is.EqualTo(cdeLib.Entities.Flags.None));
    }

    [Test]
    public void IsHashDone_Cleared_DropsTheFlag()
    {
        var e = new RootEntry();
        e.SetHash(1UL);

        e.IsHashDone = false;

        Assert.That(e.IsHashDone, Is.False);
    }

    [Test]
    public void IsHashDone_LegacyPartialHashWithoutV2Bit_ReadsAsNotDone()
    {
        var e = new RootEntry { BitFields = cdeLib.Entities.Flags.HashDone | cdeLib.Entities.Flags.PartialHash };

        Assert.That(e.IsHashDone, Is.False);
    }
}

[TestFixture]
public class BoundedRegexCacheTests
{
    [Test]
    public void ManyDistinctPatterns_EvictFromTheCacheAndStillMatch()
    {
        for (var i = 0; i < 130; i++)
        {
            var regex = new BoundedRegex($"evict-pattern-{i}-[a-z]");
            Assert.That(regex.IsMatch($"EVICT-PATTERN-{i}-q"), Is.True);
        }
    }
}
