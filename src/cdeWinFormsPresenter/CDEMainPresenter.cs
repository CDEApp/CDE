using System;
using System.Collections.Generic;
using cdeLib;

namespace cdeWinFormsPresenter
{
    public class CDEMainPresenter : ICDEMainPresenter, ICDEMainPresenterCallBacks
    {
        private readonly ICDEMainView _view;

        public CDEMainPresenter(ICDEMainView view)
        {
            _view = view;
            _view.ButtonText = "MyButton";
            _view.ButtonEnabled = true;
        }

        public object View
        {
            get { throw new NotImplementedException(); }
        }

        public void OnDoubleClickSelectedItem()
        {
            throw new NotImplementedException();
        }

        static int _count = 0;
        public void OnButtonClick()
        {
            ++_count;
            _view.ButtonText = "click " + _count;
        }
    }

    public interface IPresenter
    {
        // void Initialize(); // not sure this is required or desired.
        object View { get; }
    }

    public interface ICDEMainPresenter : IPresenter
    {
    }

    public interface ICDEMainPresenterCallBacks
    {
        void OnDoubleClickSelectedItem();
        void OnButtonClick();
    }

    public interface IView<TCallBack>
    {
        void Attach(TCallBack presenter);
    }

    public interface ICDEMainView : IView<ICDEMainPresenterCallBacks>
    {
        bool ButtonEnabled { get; set; }
        string ButtonText { get; set; }

        IList<RootEntry> GetCatalogs();
    }
}
