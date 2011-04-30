using System.Collections.Generic;
using cdeLib;
using cdeWinFormsPresenter;
using NUnit.Framework;
using Rhino.Mocks;

namespace cdeWinFormsTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class CDEMainPresenterTest
    {
        [Test]
        public void Constructor_BasicInitialise_OK()
        {
            // this test seems pointless, see comment about tests in TheHumbleDialog document. by Michael Feathers.
            // its not really a stub test, you want to check that CDEMainPresenter calls through to the desired view.
            //   I think this means its more along the lines mocks.

            var mockView = MockRepository.GenerateStub<ICDEMainView>();
            mockView.Stub(x => x.GetCatalogs())
                .Repeat.Times(1)
                .Return(new List<RootEntry>());

            var p = new CDEMainPresenter(mockView);

            Assert.That(p, Is.Not.Null);
            Assert.That(mockView.GetCatalogs(), Is.Empty);
        }

        [Test]
        public void Constructor_CheckWhatItCallsOniew_OK()
        {
            var mockView = MockRepository.GenerateStub<ICDEMainView>();
            mockView.Stub(x => x.ButtonText)
                .Repeat.Times(1)
                ;

            var p = new CDEMainPresenter(mockView);

            Assert.That(p, Is.Not.Null);
            Assert.That(mockView.GetCatalogs(), Is.Empty);

            // TODO verify call to mock ?
        }

    }
    // ReSharper restore InconsistentNaming
}
