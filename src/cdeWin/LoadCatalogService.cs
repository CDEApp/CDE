using System.Collections.Generic;
using Util;
using cdeLib;

namespace cdeWin
{
    public interface ILoadCatalogService
    {
        List<RootEntry> LoadRootEntries(IConfig config, TimeIt timeIt);
    }

    public class LoadCatalogService : ILoadCatalogService
    {
        public List<RootEntry> LoadRootEntries(IConfig config, TimeIt timeIt)
        {
            List<RootEntry> rootEntries;
            var cachePathList = new[] { ".", config.ConfigPath };
            var loaderForm = new LoaderForm(config, cachePathList, timeIt);

            try
            {
                loaderForm.ShowDialog();
            }
            finally
            {
                rootEntries = loaderForm.RootEntries;
                loaderForm.Dispose();
            }
            return rootEntries;
        }
    }
}