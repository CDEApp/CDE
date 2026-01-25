using System.Collections.Generic;
using cdeLib;
using cdeWin;
using cdeWin.Cfg;

namespace cdeWinTest;

public class TestPresenterSetSearch : CDEWinFormPresenter
{
    public TestPresenterSetSearch(ICDEWinForm form, IConfig config) : base(form, config)
    {
    }

    public int TestSetSearchResultList(List<PairDirEntry> list)
    {
        return SetSearchResultList(list);
    }
}