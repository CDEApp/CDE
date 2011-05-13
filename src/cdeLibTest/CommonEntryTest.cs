﻿using System;
using System.Collections.Generic;
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
            var mockAction = MockRepository.GenerateMock<Action<CommonEntry, DirEntry>>();
            mockAction.Stub(x => x(null, null));
            var de = new DirEntry();

            de.TraverseTreePair("", mockAction);

            mockAction.AssertWasNotCalled(x => x(null, null));
        }

        [Test]
        public void TraverseTree_SingleChildTree_CallsActionOnChild()
        {
            var de1 = new DirEntry { Name = "d1", FullPath = "Mooo"}; // only looks at Children for recurse.
            var de2 = new DirEntry { Name = "d2" };
            de1.Children.Add(de2);

            var mockAction = MockRepository.GenerateMock<Action<CommonEntry, DirEntry>>();
            mockAction.Stub(x => x(de1, de2));

            de1.TraverseTreePair("", mockAction);

            mockAction.AssertWasCalled(x => x(de1, de2));
        }

        [Test]
        public void TraverseTree_TreeHeirarchy_CallsActionOnAllChildren()
        {
            var re1 = new RootEntry { RootPath = @"Z:\" };
            var de2a = new DirEntry { Name = "d2a" };
            var de2b = new DirEntry { Name = "d2b", IsDirectory = true };
            var de2c = new DirEntry { Name = "d2c" };
            re1.Children.Add(de2a);
            re1.Children.Add(de2b);
            re1.Children.Add(de2c);
            var de3a = new DirEntry { Name = "d3a", IsDirectory = true };
            de2b.Children.Add(de3a);
            var de4a = new DirEntry { Name = "d4a" };
            de3a.Children.Add(de4a);
            re1.SetInMemoryFields();

            var mockAction = MockRepository.GenerateMock<Action<CommonEntry, DirEntry>>();
            using (mockAction.GetMockRepository().Ordered())
            {
                mockAction.Expect(x => x(re1, de2a)).Repeat.Times(1);
                mockAction.Expect(x => x(re1, de2b)).Repeat.Times(1);
                mockAction.Expect(x => x(re1, de2c)).Repeat.Times(1);
                mockAction.Expect(x => x(de2b, de3a)).Repeat.Times(1);
                mockAction.Expect(x => x(de3a, de4a)).Repeat.Times(1);
            }

            re1.TraverseTreePair("", mockAction);

            mockAction.VerifyAllExpectations();
        }

        [Test]
        public void TraverseAllTrees_TreeHeirarchy_CallsActionOnAllChildrenOnBothRoots()
        {
            var re1 = new RootEntry { RootPath = "" };
            var de2a = new DirEntry { Name = "d2a" };
            var de2b = new DirEntry { Name = "d2b", IsDirectory = true };
            var de2c = new DirEntry { Name = "d2c" };
            re1.Children.Add(de2a);
            re1.Children.Add(de2b);
            re1.Children.Add(de2c);
            var de3a = new DirEntry { Name = "d3a", IsDirectory = true };
            de2b.Children.Add(de3a);
            var de4a = new DirEntry { Name = "d4a" };
            de3a.Children.Add(de4a);

            // same structure as re1 with a different root path
            var re2 = new RootEntry { RootPath = "2" };
            var bde2a = new DirEntry { Name = "d2a" };
            var bde2b = new DirEntry { Name = "d2b", IsDirectory = true };
            var bde2c = new DirEntry { Name = "d2c" };
            re2.Children.Add(bde2a);
            re2.Children.Add(bde2b);
            re2.Children.Add(bde2c);
            var bde3a = new DirEntry { Name = "d3a", IsDirectory = true };
            bde2b.Children.Add(bde3a);
            var bde4a = new DirEntry { Name = "d4a" };
            bde3a.Children.Add(bde4a);

            var mockAction = MockRepository.GenerateMock<Action<CommonEntry, DirEntry>>();
            using (mockAction.GetMockRepository().Ordered())
            {
                mockAction.Expect(x => x(re1, de2a)).Repeat.Times(1);
                mockAction.Expect(x => x(re1, de2b)).Repeat.Times(1);
                mockAction.Expect(x => x(re1, de2c)).Repeat.Times(1);
                mockAction.Expect(x => x(de2b, de3a)).Repeat.Times(1);
                mockAction.Expect(x => x(de3a, de4a)).Repeat.Times(1);

                mockAction.Expect(x => x(re2, bde2a)).Repeat.Times(1);
                mockAction.Expect(x => x(re2, bde2b)).Repeat.Times(1);
                mockAction.Expect(x => x(re2, bde2c)).Repeat.Times(1);
                mockAction.Expect(x => x(bde2b, bde3a)).Repeat.Times(1);
                mockAction.Expect(x => x(bde3a, bde4a)).Repeat.Times(1);
            }

            CommonEntry.TraverseAllTreesPair(new List<RootEntry> { re1, re2 }, mockAction);

            mockAction.VerifyAllExpectations();
        }
    }
    // ReSharper restore InconsistentNaming
}
