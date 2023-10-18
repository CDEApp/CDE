using cdeLib.Entities;
using cdeLib.Infrastructure.Config;

namespace cdeLibTest;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1102:Make class static.", Justification = "<Pending>")]
public class RootEntryTestBase
{
    // ReSharper disable InconsistentNaming

    /// <summary>
    /// This does not create a full RootEntry - it does setup SetInMemoryFields().
    /// This test structure.
    ///  Z:\
    ///  Z:\d2a (size 11)
    ///  Z:\d2b\
    ///  Z:\d2b\d3a\
    ///  Z:\d2b\d3a\d4a (size 17)
    ///  Z:\d2c (size 0)
    /// </summary>
    public static RootEntry NewTestRootEntry(IConfiguration config, out DirEntry de2a, out DirEntry de2b, out DirEntry de2c, out DirEntry de3a, out DirEntry de4a)
    {
        var re1 = new RootEntry(config) { Path = @"Z:\" };
        de2a = new DirEntry { Path = "d2a", Size = 11 };
        de2b = new DirEntry(true) { Path = "d2b" };
        de2c = new DirEntry { Path = "d2c" };
        re1.AddChild(de2c);
        re1.AddChild(de2a);
        re1.AddChild(de2b);

        de3a = new DirEntry(true) { Path = "d3a" };
        de2b.AddChild(de3a);

        de4a = new DirEntry { Path = "d4a", Size = 17 };
        de3a.AddChild(de4a);

        return re1;
    }

    //public static RootEntry NewTestRootEntrySetup(out DirEntry de2a, out DirEntry de2b, out DirEntry de2c, out DirEntry de3a, out DirEntry de4a)
    //{
    //    var re1 = NewTestRootEntry(out de2a, out de2b, out de2c, out de3a, out de4a);
    //    re1.SetInMemoryFields();
    //    return re1;
    //}

    // ReSharper restore InconsistentNaming
}