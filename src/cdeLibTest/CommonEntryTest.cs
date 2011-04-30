using System;
using cdeLib;
using NUnit.Framework;
using Rhino.Mocks;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    class CommonEntryTest
    {
        [Test]
        public void Constructor_Minimal_OK()
        {
            var a = new CommonEntryTestStub();

            Assert.That(a, Is.Not.Null);
        }

        [Test]
        public void TraverseTree_EmptyTree_ActionNotRun()
        {
            var mockAction = MockRepository.GenerateMock<Action<string, DirEntry>>();
            mockAction.Stub(x => x("", null));
            var de = new DirEntry();

            de.TraverseTree("", mockAction);

            mockAction.AssertWasNotCalled(x => x("", null));
        }

        [Test]
        public void TraverseTree_SingleChildTree_CallsActionOnChild()
        {
            var de1 = new DirEntry { Name = "d1" };// only looks at Children for recurse.
            var de2 = new DirEntry { Name = "d2" };
            de1.Children.Add(de2);

            var mockAction = MockRepository.GenerateMock<Action<string, DirEntry>>();
            mockAction.Stub(x => x("d2", de2));

            de1.TraverseTree("", mockAction);

            mockAction.AssertWasCalled(x => x("d2", de2));
        }

        [Test]
        public void TraverseTree_TreeHeirArchy_CallsActionOnAllChildren()
        {
            var de1 = new DirEntry { Name = "d1" }; // this just the root to work 'from'
            var de2a = new DirEntry { Name = "d2a" };
            var de2b = new DirEntry { Name = "d2b", IsDirectory = true };
            var de2c = new DirEntry { Name = "d2c" };
            de1.Children.Add(de2a);
            de1.Children.Add(de2b);
            de1.Children.Add(de2c);
            var de3a = new DirEntry { Name = "d3a", IsDirectory = true };
            de2b.Children.Add(de3a);
            var de4a = new DirEntry { Name = "d4a" };
            de3a.Children.Add(de4a);

            var mockAction = MockRepository.GenerateMock<Action<string, DirEntry>>();
            using (mockAction.GetMockRepository().Ordered())
            {
                mockAction.Expect(x => x("d2a", de2a)).Repeat.Times(1);
                mockAction.Expect(x => x("d2b", de2b)).Repeat.Times(1);
                mockAction.Expect(x => x("d2c", de2c)).Repeat.Times(1);
                mockAction.Expect(x => x(@"d2b\d3a", de3a)).Repeat.Times(1);
                mockAction.Expect(x => x(@"d2b\d3a\d4a", de4a)).Repeat.Times(1);
            }

            de1.TraverseTree("", mockAction);

            mockAction.VerifyAllExpectations();
        }
    }
    // ReSharper restore InconsistentNaming
}
