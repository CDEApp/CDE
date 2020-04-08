using cdeLib;
using NUnit.Framework;
using NSubstitute;
using Shouldly;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    class CommonEntryTest_TraverseTree : RootEntryTestBase
    {
        [Test]
        public void Constructor_Minimal_OK()
        {
            var a = new CommonEntryTestStub();

            Assert.That(a, Is.Not.Null);
        }

        [Ignore("This crashes when run")]
        [Test]
        public void TraverseTree_EmptyTree_ActionNotRun()
        {
            // This fails with error.
            // Castle.DynamicProxy.InvalidProxyConstructorArgumentsException :
            //   Can not instantiate proxy of class: System.Object.
            // Could not find a parameterless constructor.
            var mockAction = Substitute.For<TraverseFunc>();
            var de = new DirEntry();

            de.TraverseTreePair(mockAction);

            mockAction.DidNotReceive().Invoke(Arg.Any<ICommonEntry>(), Arg.Any<DirEntry>());
        }

        [Test]
        public void TraverseTree_EmptyTree_ActionNotRun_Alternate()
        {
            var actionCalled = false;
            var de = new DirEntry();

            de.TraverseTreePair((_ce, _de) =>
            {
                actionCalled = true;
                return false;
            });

            actionCalled.ShouldBe(false);
        }

        [Ignore("This crashes when run")]
        [Test]
        public void TraverseTree_SingleChildTree_CallsActionOnChild()
        {
            // only looks at Children for recurse.
            var de1 = new DirEntry(true) { Path = "d1", FullPath = "Mooo" };
            var de2 = new DirEntry { Path = "d2" };
            de1.AddChild(de2);
            var mockAction = Substitute.For<TraverseFunc>();

            de1.TraverseTreePair(mockAction);

            mockAction.Received().Invoke(Arg.Any<ICommonEntry>(), Arg.Any<DirEntry>());
        }

        [Test]
        public void TraverseTree_SingleChildTree_CallsActionOnChild_Alternate()
        {
            var actionCalled = false;
            // only looks at Children for recurse.
            var de1 = new DirEntry(true) { Path = "d1", FullPath = "Mooo" };
            var de2 = new DirEntry { Path = "d2" };
            de1.AddChild(de2);

            de1.TraverseTreePair((_ce, _de) =>
            {
                actionCalled = true;
                return false;
            });

            actionCalled.ShouldBe(true);
        }
    }
    // ReSharper restore InconsistentNaming
}
