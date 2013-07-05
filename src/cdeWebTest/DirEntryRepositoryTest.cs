using System;
using System.Linq;
using System.Web.Http;
using NSubstitute;
using NUnit.Framework;
using cdeWeb;
using cdeWeb.Models;


namespace cdeWebTest
{
    [TestFixture]
    public class DirEntryRepositoryTest
    {
        private DirEntryRepository _candidateDER;

        readonly DirEntry[] _entries = new[] 
        {
            new DirEntry { Name = "Tomato Soup", Size = 1, Path = @"C:\Tomato Soup", Modified = new DateTime(2013, 01, 01, 09, 10, 11, DateTimeKind.Local) },
            new DirEntry { Name = "Yo-yo", Size = 3, Path = @"C:\Yo-yo", Modified = new DateTime(2013, 01, 02, 09, 10, 12, DateTimeKind.Local) },
            new DirEntry { Name = "Hammer", Size = 4, Path = @"C:\Hammer", Modified = new DateTime(2013, 01, 03, 09, 10, 13, DateTimeKind.Local) } ,
            new DirEntry { Name = "Tomato Soup2", Size = 1, Path = @"C:\Tomato Soup2", Modified = new DateTime(2013, 01, 04, 09, 10, 14, DateTimeKind.Local) },
            new DirEntry { Name = "Yo-yo2", Size = 3, Path = @"C:\Yo-yo2", Modified = new DateTime(2013, 01, 04, 09, 10, 15, DateTimeKind.Local) },
            new DirEntry { Name = "Hammer2", Size = 4, Path = @"C:\Hammer2", Modified = new DateTime(2013, 01, 05, 09, 10, 16, DateTimeKind.Local) }
        };

        private IDataStore _dataStore;

        [SetUp]
        public void BeforeEachTest()
        {
            _dataStore = Substitute.For<IDataStore>();

            _candidateDER = new DirEntryRepository(_dataStore);
        }

        //p => p.Name.Contains(query)

        //[Test]
        //public void Query_GetAll_Returns3()
        //{
        //    _dataStore.Search("").Returns(_entries);

        //    Assert.That(_candidateDER.GetQuery(string.Empty).Value.Count(), Is.EqualTo(6));
        //}

        //[Test]
        //public void Query_Get_NotExisting_ThrowsException()
        //{
        //    Assert.Throws<HttpResponseException>(() => _candidateDER.GetQuery("NotExisting"));
        //}

        //[Test]
        //public void Query_Get_FullMatch_Returns1()
        //{
        //    _dataStore.Search("Yo-yo").Returns(new[] { _entries[1] });

        //    Assert.That(_candidateDER.GetQuery("Yo-yo").First().Name, Is.EqualTo("Yo-yo"));
        //    // i like this do others ?
        //    //_candidateDER.Get("Yo-yo").Name.Should(Be.EqualTo("Yo-yo"));
        //}

        //[Test]
        //public void Query_Get_SubString_Returns1()
        //{
        //    _dataStore.Search("yo").Returns(new[] { _entries[1] });

        //    Assert.That(_candidateDER.GetQuery("yo").First().Name, Is.EqualTo("Yo-yo"));
        //}
    }
}
