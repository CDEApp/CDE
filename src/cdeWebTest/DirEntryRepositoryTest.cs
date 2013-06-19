using System.Linq;
using System.Web.Http;
using NUnit.Framework;
using cdeWeb.Models;


namespace cdeWebTest
{
    [TestFixture]
    public class DirEntryRepositoryTest
    {
        private DirEntryRepository _candidateDER;

        [SetUp]
        public void BeforeEachTest()
        {
            _candidateDER = new DirEntryRepository();
        }

        [Test]
        public void Query_GetAll_Returns3()
        {
            Assert.That(_candidateDER.GetAll().Count(), Is.EqualTo(3));
        }

        [Test]
        public void Query_Get_NotExisting_ThrowsException()
        {
            Assert.Throws<HttpResponseException>(() => _candidateDER.GetQuery("NotExisting"));
        }

        [Test]
        public void Query_Get_FullMatch_Returns1()
        {
            Assert.That(_candidateDER.GetQuery("Yo-yo").First().Name, Is.EqualTo("Yo-yo"));
            // i like this do others ?
            //_candidateDER.Get("Yo-yo").Name.Should(Be.EqualTo("Yo-yo"));
        }

        [Test]
        public void Query_Get_SubString_Returns1()
        {
            Assert.That(_candidateDER.GetQuery("yo").First().Name, Is.EqualTo("Yo-yo"));
        }
    }
}
