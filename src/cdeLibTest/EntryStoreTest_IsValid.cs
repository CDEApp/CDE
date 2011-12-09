using System;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class EntryStoreTest_IsValid : EntryTestBase
    {
        [Test][ExpectedException(typeof(Exception), ExpectedMessage = "Entry Store must have Root set to be valid.")]
        public void IsValid_NoRootSet_ThrowsException()
        {
            var entryStore = new EntryStore();

            entryStore.IsValid();
        }

        [Test][ExpectedException(typeof(Exception), ExpectedMessage = "Entry Store Root must have valid RootIndex.")]
        public void IsValid_NoRootIndexSet_ThrowsException()
        {
            var entryStore = new EntryStore();
            var Root = new RootEntry();
            entryStore.Root = Root;

            entryStore.IsValid();
        }

        [Test][ExpectedException(typeof(Exception), ExpectedMessage = "Entry Store Root must have valid Path.")]
        public void IsValid_NoRootPathSet_ThrowsException()
        {
            var entryStore = new EntryStore();
            var Root = new RootEntry { RootIndex = 1 };
            entryStore.Root = Root;

            entryStore.IsValid();
        }

        [Test][ExpectedException(typeof(Exception), ExpectedMessage = "Entry Store Root must have valid DefaultFileName.")]
        public void IsValid_NoDefaultFileNameSet_ThrowsException()
        {
            var entryStore = new EntryStore();
            var Root = new RootEntry { RootIndex = 1, Path = @"C:\" };
            entryStore.Root = Root;

            entryStore.IsValid();
        }

        [Test][ExpectedException(typeof(IndexOutOfRangeException), ExpectedMessage = "Out of allocated Entry store range.")]
        public void IsValid_NoIndexesSetup_ThrowsException()
        {
            var entryStore = new EntryStore();
            var Root = new RootEntry { RootIndex = 1, Path = @"C:\", DefaultFileName = "save.cde" };
            entryStore.Root = Root;

            entryStore.IsValid();
        }

        [Test][ExpectedException(typeof(Exception), ExpectedMessage = "Entry Store Root Index Entry must be a directory.")]
        public void IsValid_RootEntryNotDirectory_ThrowsException()
        {
            var entryStore = new EntryStore();
            var rootIndex = entryStore.AddEntry();
            var Root = new RootEntry { RootIndex = rootIndex, Path = @"C:\", DefaultFileName = "save.cde" };
            entryStore.Root = Root;

            entryStore.IsValid();
        }

        [Test][ExpectedException(typeof(Exception), ExpectedMessage = "Entry Store Root Index Entry must have non empty FullPath set.")]
        public void IsValid_RootEntryMustHaveFullPathSet_ThrowsException()
        {
            var entryStore = new EntryStore();
            var rootIndex = entryStore.AddEntry(null, "", 0, DateTime.UtcNow, true);
            var Root = new RootEntry { RootIndex = rootIndex, Path = @"C:\", DefaultFileName = "save.cde" };
            entryStore.Root = Root;

            entryStore.IsValid();
        }

        [Test]
        public void IsValid_All_OK()
        {
            var entryStore = new EntryStore();
            var rootIndex = entryStore.AddEntry(null, @"C:\", 0, DateTime.UtcNow, true);
            var Root = new RootEntry { RootIndex = rootIndex, Path = @"C:\", DefaultFileName = "save.cde" };
            entryStore.Root = Root;

            entryStore.IsValid();
        }
    }
    // ReSharper restore InconsistentNaming
}