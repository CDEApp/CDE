using System.Collections.Generic;
using cdeLib;
using NUnit.Framework;
using Rhino.Mocks;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    class CommonEntryTest_TraverseTree
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
            var mockAction = MockRepository.GenerateMock<TraverseFunc>();
            mockAction.Stub(x => x(null, null));
            var de = new DirEntry();

            de.TraverseTreePair(mockAction);

            mockAction.AssertWasNotCalled(x => x(null, null));
        }

        [Test]
        public void TraverseTree_SingleChildTree_CallsActionOnChild()
        {
            var de1 = new DirEntry(true) { Path = "d1", FullPath = "Mooo" }; // only looks at Children for recurse.
            var de2 = new DirEntry { Path = "d2" };
            de1.Children.Add(de2);

            var mockAction = MockRepository.GenerateMock<TraverseFunc>();
            mockAction.Stub(x => x(de1, de2)).Return(true);

            de1.TraverseTreePair(mockAction);

            mockAction.AssertWasCalled(x => x(de1, de2));
        }

        [Test]
        public void TraverseTree_TreeHeirarchy_CallsActionOnAllChildren()
        {
            DirEntry de2a;
            DirEntry de2b;
            DirEntry de2c;
            DirEntry de3a;
            DirEntry de4a;
            var re1 = NewTestRootEntry(out de2a, out de2b, out de2c, out de3a, out de4a);

            var mockAction = MockRepository.GenerateMock<TraverseFunc>();
            using (mockAction.GetMockRepository().Ordered())
            {
                mockAction.Expect(x => x(re1, de2a)).Repeat.Times(1).Return(true);
                mockAction.Expect(x => x(re1, de2b)).Repeat.Times(1).Return(true);
                mockAction.Expect(x => x(re1, de2c)).Repeat.Times(1).Return(true);
                mockAction.Expect(x => x(de2b, de3a)).Repeat.Times(1).Return(true);
                mockAction.Expect(x => x(de3a, de4a)).Repeat.Times(1).Return(true);
            }

            ((CommonEntry) re1).TraverseTreePair(mockAction);

            mockAction.VerifyAllExpectations();
        }


        [Test]
        public void TraverseTree_TreeHeirarchy_CallsActionOnAllChildrenBeforeFuncReturnsFalse()
        {
            DirEntry de2a;
            DirEntry de2b;
            DirEntry de2c;
            DirEntry de3a;
            DirEntry de4a;
            var re1 = NewTestRootEntry(out de2a, out de2b, out de2c, out de3a, out de4a);

            var mockAction = MockRepository.GenerateMock<TraverseFunc>();
            using (mockAction.GetMockRepository().Ordered())
            {
                mockAction.Expect(x => x(re1, de2a)).Repeat.Times(1).Return(true);
                mockAction.Expect(x => x(re1, de2b)).Repeat.Times(1).Return(true);
                mockAction.Expect(x => x(re1, de2c)).Repeat.Times(1).Return(false);
            }

            ((CommonEntry)re1).TraverseTreePair(mockAction);

            mockAction.VerifyAllExpectations();
        }

        public static RootEntry NewTestRootEntry(out DirEntry de2a, out DirEntry de2b, out DirEntry de2c, out DirEntry de3a, out DirEntry de4a)
        {
            var re1 = new RootEntry {Path = @"Z:\" };
            de2a = new DirEntry {Path = "d2a" };
            de2b = new DirEntry(true) {Path = "d2b" };
            de2c = new DirEntry {Path = "d2c"};
            re1.Children.Add(de2a);
            re1.Children.Add(de2b);
            re1.Children.Add(de2c);
            de3a = new DirEntry(true) { Path = "d3a" };
            de2b.Children.Add(de3a);
            de4a = new DirEntry {Path = "d4a"};
            de3a.Children.Add(de4a);
            re1.SetInMemoryFields();
            return re1;
        }

        [Test]
        public void TraverseAllTrees_TreeHeirarchy_CallsActionOnAllChildrenOnBothRoots()
        {
            DirEntry de2a;
            DirEntry de2b;
            DirEntry de2c;
            DirEntry de3a;
            DirEntry de4a;
            var re1 = NewTestRootEntry(out de2a, out de2b, out de2c, out de3a, out de4a);

            DirEntry bde2a;
            DirEntry bde2b;
            DirEntry bde2c;
            DirEntry bde3a;
            DirEntry bde4a;
            var re2 = NewTestRootEntry(out bde2a, out bde2b, out bde2c, out bde3a, out bde4a);
            // same structure as re1 with a different root path
            re2.Path = "2";

            var mockAction = MockRepository.GenerateMock<TraverseFunc>();
            using (mockAction.GetMockRepository().Ordered())
            {
                mockAction.Expect(x => x(re1, de2a)).Repeat.Times(1).Return(true);
                mockAction.Expect(x => x(re1, de2b)).Repeat.Times(1).Return(true);
                mockAction.Expect(x => x(re1, de2c)).Repeat.Times(1).Return(true);
                mockAction.Expect(x => x(de2b, de3a)).Repeat.Times(1).Return(true);
                mockAction.Expect(x => x(de3a, de4a)).Repeat.Times(1).Return(true);

                mockAction.Expect(x => x(re2, bde2a)).Repeat.Times(1).Return(true);
                mockAction.Expect(x => x(re2, bde2b)).Repeat.Times(1).Return(true);
                mockAction.Expect(x => x(re2, bde2c)).Repeat.Times(1).Return(true);
                mockAction.Expect(x => x(bde2b, bde3a)).Repeat.Times(1).Return(true);
                mockAction.Expect(x => x(bde3a, bde4a)).Repeat.Times(1).Return(true);
            }

            CommonEntry.TraverseTreePair(new List<RootEntry> { re1, re2 }, mockAction);

            mockAction.VerifyAllExpectations();
        }
    }
    // ReSharper restore InconsistentNaming
}
