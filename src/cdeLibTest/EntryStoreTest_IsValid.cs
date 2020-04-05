namespace cdeLibTest
{
    using NUnit.Framework;

    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class EntryStoreTest_IsValid : EntryTestBase
    {
        // public void IsValid_NoRootSet_ThrowsException()
        // {
        //     var entryStore = new EntryStore();
        //
        //     Assert.Throws<Exception>(() => entryStore.IsValid(), "Entry Store must have Root set to be valid.");
        // }
        //
        // [Test]
        // public void IsValid_All_OK()
        // {
        //     var entryStore = new EntryStore();
        //     var rootIndex = entryStore.AddEntry(null, @"C:\", 0, DateTime.UtcNow, true);
        //     var root = new RootEntry { RootIndex = rootIndex, Path = @"C:\", DefaultFileName = "save.cde" };
        //     entryStore.Root = root;
        //
        //     entryStore.IsValid();
        // }
        //
        // [Test]
        // public void IsValid_NoDefaultFileNameSet_ThrowsException()
        // {
        //     var entryStore = new EntryStore();
        //     var root = new RootEntry { RootIndex = 1, Path = @"C:\" };
        //     entryStore.Root = root;
        //
        //     Assert.Throws<Exception>(() => entryStore.IsValid(), "Entry Store Root must have valid DefaultFileName.");
        // }
        //
        // [Test]
        // public void IsValid_NoIndexesSetup_ThrowsException()
        // {
        //     var entryStore = new EntryStore();
        //     var root = new RootEntry { RootIndex = 1, Path = @"C:\", DefaultFileName = "save.cde" };
        //     entryStore.Root = root;
        //
        //     Assert.Throws<IndexOutOfRangeException>(() => entryStore.IsValid(), "Out of allocated Entry store range.");
        // }
        //
        // [Test]
        // public void IsValid_NoRootIndexSet_ThrowsException()
        // {
        //     var entryStore = new EntryStore();
        //     var root = new RootEntry();
        //     entryStore.Root = root;
        //
        //     Assert.Throws<Exception>(() => entryStore.IsValid(), "Entry Store Root must have valid RootIndex.");
        // }
        //
        // [Test]
        // public void IsValid_NoRootPathSet_ThrowsException()
        // {
        //     var entryStore = new EntryStore();
        //     var root = new RootEntry { RootIndex = 1 };
        //     entryStore.Root = root;
        //
        //     Assert.Throws<Exception>(() => entryStore.IsValid(), "Entry Store Root must have valid Path.");
        // }
        //
        // [Test]
        // public void IsValid_RootEntryMustHaveFullPathSet_ThrowsException()
        // {
        //     var entryStore = new EntryStore();
        //     var rootIndex = entryStore.AddEntry(null, string.Empty, 0, DateTime.UtcNow, true);
        //     var root = new RootEntry { RootIndex = rootIndex, Path = @"C:\", DefaultFileName = "save.cde" };
        //     entryStore.Root = root;
        //
        //     Assert.Throws<Exception>(() => entryStore.IsValid(), "Entry Store Root Index Entry must have non empty FullPath set.");
        // }
        //
        // [Test]
        // public void IsValid_RootEntryNotDirectory_ThrowsException()
        // {
        //     var entryStore = new EntryStore();
        //     var rootIndex = entryStore.AddEntry();
        //     var root = new RootEntry { RootIndex = rootIndex, Path = @"C:\", DefaultFileName = "save.cde" };
        //     entryStore.Root = root;
        //
        //     Assert.Throws<Exception>(() => entryStore.IsValid(), "Entry Store Root Index Entry must be a directory.");
        // }
    }

    // ReSharper restore InconsistentNaming
}