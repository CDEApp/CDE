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
            de1.Children.Add(de2);
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
            de1.Children.Add(de2);

            de1.TraverseTreePair((_ce, _de) =>
            {
                actionCalled = true;
                return false;
            });

            actionCalled.ShouldBe(true);
        }

//        [Test]
//        public void TraverseTree_TreeHeirarchy_CallsActionOnAllChildren()
//        {
//            DirEntry de2a;
//            DirEntry de2b;
//            DirEntry de2c;
//            DirEntry de3a;
//            DirEntry de4a;
//            var re1 = NewTestRootEntry(out de2a, out de2b, out de2c, out de3a, out de4a);
//
//            var mockAction = Substitute.For<TraverseFunc>();
//            using (mockAction.GetMockRepository().Ordered())
//            {
//                mockAction.Expect(x => x(re1, de2c)).Repeat.Times(1).Return(true);
//                mockAction.Expect(x => x(re1, de2a)).Repeat.Times(1).Return(true);
//                mockAction.Expect(x => x(re1, de2b)).Repeat.Times(1).Return(true);
//                mockAction.Expect(x => x(de2b, de3a)).Repeat.Times(1).Return(true);
//                mockAction.Expect(x => x(de3a, de4a)).Repeat.Times(1).Return(true);
//            }
//
//            ((CommonEntry) re1).TraverseTreePair(mockAction);
//
//            mockAction.VerifyAllExpectations();
//        }
//
//
//        [Test]
//        public void TraverseTree_TreeHeirarchy_CallsActionOnAllChildrenBeforeFuncReturnsFalse()
//        {
//            DirEntry de2a;
//            DirEntry de2b;
//            DirEntry de2c;
//            DirEntry de3a;
//            DirEntry de4a;
//            var re1 = NewTestRootEntry(out de2a, out de2b, out de2c, out de3a, out de4a);
//
//            var mockAction = Substitute.For<TraverseFunc>();
//            using (mockAction.GetMockRepository().Ordered())
//            {
//                mockAction.Expect(x => x(re1, de2c)).Repeat.Times(1).Return(true);
//                mockAction.Expect(x => x(re1, de2a)).Repeat.Times(1).Return(true);
//                mockAction.Expect(x => x(re1, de2b)).Repeat.Times(1).Return(false);
//            }
//
//            ((CommonEntry)re1).TraverseTreePair(mockAction);
//
//            mockAction.VerifyAllExpectations();
//        }
//
//        [Test]
//        public void TraverseAllTrees_TreeHeirarchy_CallsActionOnAllChildrenOnBothRoots()
//        {
//            DirEntry de2a;
//            DirEntry de2b;
//            DirEntry de2c;
//            DirEntry de3a;
//            DirEntry de4a;
//            var re1 = NewTestRootEntry(out de2a, out de2b, out de2c, out de3a, out de4a);
//
//            DirEntry bde2a;
//            DirEntry bde2b;
//            DirEntry bde2c;
//            DirEntry bde3a;
//            DirEntry bde4a;
//            var re2 = NewTestRootEntry(out bde2a, out bde2b, out bde2c, out bde3a, out bde4a);
//            // same structure as re1 with a different root path
//            re2.Path = "2";
//
//            var mockAction = Substitute.For<TraverseFunc>();
//            using (mockAction.GetMockRepository().Ordered())
//            {
//                mockAction.Expect(x => x(re1, de2c)).Repeat.Times(1).Return(true);
//                mockAction.Expect(x => x(re1, de2a)).Repeat.Times(1).Return(true);
//                mockAction.Expect(x => x(re1, de2b)).Repeat.Times(1).Return(true);
//                mockAction.Expect(x => x(de2b, de3a)).Repeat.Times(1).Return(true);
//                mockAction.Expect(x => x(de3a, de4a)).Repeat.Times(1).Return(true);
//
//                mockAction.Expect(x => x(re2, bde2c)).Repeat.Times(1).Return(true);
//                mockAction.Expect(x => x(re2, bde2a)).Repeat.Times(1).Return(true);
//                mockAction.Expect(x => x(re2, bde2b)).Repeat.Times(1).Return(true);
//                mockAction.Expect(x => x(bde2b, bde3a)).Repeat.Times(1).Return(true);
//                mockAction.Expect(x => x(bde3a, bde4a)).Repeat.Times(1).Return(true);
//            }
//
//            CommonEntry.TraverseTreePair(new List<RootEntry> { re1, re2 }, mockAction);
//
//            mockAction.VerifyAllExpectations();
//        }
    }
    // ReSharper restore InconsistentNaming
}
