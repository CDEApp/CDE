using System.Collections.Generic;
using cdeLib.Entities;
using cdeWin.Cfg;
using Serilog;

namespace cdeWin;

public interface ILoadCatalogService
{
    List<RootEntry> LoadRootEntries(IConfig config);
}

public class LoadCatalogService : ILoadCatalogService
{
    private readonly ILogger _logger;

    public LoadCatalogService(ILogger logger)
    {
        _logger = logger;
    }

    public List<RootEntry> LoadRootEntries(IConfig config)
    {
        List<RootEntry> rootEntries;
        var cachePathList = new[] { ".", config.ConfigPath };
        var loaderForm = new LoaderForm(config, cachePathList, _logger);

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