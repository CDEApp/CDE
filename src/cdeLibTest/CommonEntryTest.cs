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

    }
    // ReSharper restore InconsistentNaming
}
