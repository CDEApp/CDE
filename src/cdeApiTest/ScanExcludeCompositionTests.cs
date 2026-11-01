using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cdeApi;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Serilog;

namespace cdeApiTest;

/// <summary>
/// The sidecar's composed <see cref="ICatalogSession"/> reads AppConfig:Scan from the live configuration on every
/// scan (#163): create honours ExcludePaths, and a rescan sees a later change without a restart.
/// </summary>
[TestFixture]
public class ScanExcludeCompositionTests
{
    private string _dir;
    private string _data;
    private string _skip;
    private string _cat;

    [SetUp]
    public void SetUp()
    {
        _dir = Directory.CreateTempSubdirectory("cdeapi-scanexclude").FullName;
        _cat = Path.Combine(_dir, "cat");
        _data = Path.Combine(_dir, "data");
        _skip = Path.Combine(_data, "skip");
        Directory.CreateDirectory(_cat);
        Directory.CreateDirectory(_skip);
        File.WriteAllText(Path.Combine(_data, "keep.txt"), "k");
        File.WriteAllText(Path.Combine(_skip, "inner.txt"), "i");
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { /* ignore */ }
    }

    [Test]
    public async Task Create_and_rescan_use_the_current_AppConfig_Scan_from_configuration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("AppConfig:Scan:ExcludePaths:0", _skip),
                new System.Collections.Generic.KeyValuePair<string, string>("AppConfig:Scan:UseDefaultExcludes", "false"),
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddCdeApi(Substitute());
        using var provider = services.BuildServiceProvider();
        var session = provider.GetRequiredService<ICatalogSession>();
        await session.LoadAsync(_cat);

        var created = await session.CreateCatalogAsync(new CreateCatalogRequest { RootPath = _data, FileName = "x" });
        Assert.That(created.Entries, Is.EqualTo(2)); // skip dir + keep.txt; inner.txt not scanned

        config["AppConfig:Scan:ExcludePaths:0"] = null;
        var rescanned = await session.RescanAsync(created.CatalogId);
        Assert.That(rescanned.Entries, Is.EqualTo(3));
    }

    private static ILogger Substitute() => new LoggerConfiguration().CreateLogger();
}
