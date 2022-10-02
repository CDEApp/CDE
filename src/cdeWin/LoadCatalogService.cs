using System.Collections.Generic;
using Util;
using cdeLib.Entities;
using Serilog;

namespace cdeWin;

public interface ILoadCatalogService
{
    List<RootEntry> LoadRootEntries(IConfig config, TimeIt timeIt);
}

public class LoadCatalogService : ILoadCatalogService
{
    private readonly ILogger _logger;

    public LoadCatalogService(ILogger logger)
    {
        _logger = logger;
    }

    public List<RootEntry> LoadRootEntries(IConfig config, TimeIt timeIt)
    {
        List<RootEntry> rootEntries;
        var cachePathList = new[] { ".", config.ConfigPath };
        var loaderForm = new LoaderForm(config, cachePathList, timeIt, _logger);

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