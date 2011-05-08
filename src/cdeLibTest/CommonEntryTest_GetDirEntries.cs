using System.Collections.Generic;
using System.Linq;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class CommonEntryTest_GetDirEntries
    {
        [Test]
        public void GetDirEntries_NoRootEntries_ReturnsEmptyEnumerator()
        {
            var rootEntries = new List<RootEntry>();

            var deEnum = CommonEntry.GetDirEntries(rootEntries);

            Assert.That(deEnum.Any(), Is.False);
        }

        [Test]
        public void GetDirEntries_EmptyRootEntry_ReturnsEmptyEnumerator()
        {
            var re = new RootEntry { RootPath = @"C:\" };
            var rootEntries = new List<RootEntry> { re };

            var deEnum = CommonEntry.GetDirEntries(rootEntries);

            Assert.That(deEnum.Any(), Is.False);
        }

        [Test]
        public void GetDirEntries_RootEntryOneDe_ReturnsEnumeratorWithAvailableData()
        {
            var re = new RootEntry { RootPath = @"C:\" };
            var de1 = new DirEntry { Name = @"de1" };
            re.Children.Add(de1);
            var rootEntries = new List<RootEntry> { re };

            var deEnum = CommonEntry.GetDirEntries(rootEntries);

            Assert.That(deEnum.Any(), Is.True);

            //foreach (var flatDE in CommonEntry.GetDirEntries(rootEntries))
            //{
            //}
        }

        [Test]
        public void GetDirEntries_RootEntryOneDe_ReturnsThatOneDe()
        {
            var re = new RootEntry {RootPath = @"C:\"};
            var de1 = new DirEntry {Name = @"de1"};
            re.Children.Add(de1);
            var rootEntries = new List<RootEntry> {re};
            var expectFde = new FlatDirEntryDTO(@"C:\de1", de1);

            var deEnum = CommonEntry.GetDirEntries(rootEntries);

            var fde = deEnum.First();
            Assert.That(fde.DirEntry, Is.EqualTo(expectFde.DirEntry));
            Assert.That(fde.FilePath, Is.EqualTo(expectFde.FilePath));
        }

        [Ignore("Incomplete")]
        [Test]
        public void GetDirEntries_DirectoryWithOneFile_ReturnsDirThenFile()
        {
            var re = new RootEntry { RootPath = @"C:\" };
            var de1 = new DirEntry { Name = @"de1", IsDirectory = true };
            var fe2 = new DirEntry { Name = @"fe2" };
            de1.Children.Add(fe2);
            re.Children.Add(de1);
            var rootEntries = new List<RootEntry> { re };

            var expectFde = new FlatDirEntryDTO(@"C:\de1", de1);
            var expectFde2 = new FlatDirEntryDTO(@"C:\de1\fe2", fe2);
            var expected = new List<FlatDirEntryDTO> { expectFde, expectFde2 };

            var deEnum = CommonEntry.GetDirEntries(rootEntries);
            var expectEnum = expected.GetEnumerator();

            
            //while (deEnum.MoveNext() && expectEnum.MoveNext())
            //{
                
            //}
            
            //var fde = deEnum.First();
            //Assert.That(fde.DirEntry, Is.EqualTo(expectFde.DirEntry));
            //Assert.That(fde.FilePath, Is.EqualTo(expectFde.FilePath));
            //var fde = deEnum.Ne();
            //Assert.That(fde.DirEntry, Is.EqualTo(expectFde.DirEntry));
            //Assert.That(fde.FilePath, Is.EqualTo(expectFde.FilePath));
        }
    }
    // ReSharper restore InconsistentNaming
}