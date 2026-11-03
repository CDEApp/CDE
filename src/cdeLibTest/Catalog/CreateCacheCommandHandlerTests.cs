using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Catalog;
using cdeLib.Entities;
using cdeLib.Infrastructure.Config;
using NSubstitute;
using NUnit.Framework;
using SlimMessageBus;

namespace cdeLibTest.Catalog;

[TestFixture]
[NonParallelizable]
public class CreateCacheCommandHandlerTests
{
    private string _dir;
    private string _data;
    private string _previousDirectory;

    [SetUp]
    public void SetUp()
    {
        _dir = Directory.CreateTempSubdirectory("cde-scan-handler").FullName;
        _data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(Path.Combine(_data, "kept"));
        Directory.CreateDirectory(Path.Combine(_data, "node_modules"));
        File.WriteAllText(Path.Combine(_data, "kept", "a.txt"), "a");
        File.WriteAllText(Path.Combine(_data, "node_modules", "b.txt"), "b");
        _previousDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.SetCurrentDirectory(_previousDirectory);
        Directory.Delete(_dir, true);
    }

    [Test]
    public async Task OnHandle_WritesACatalogThatRecordsButSkipsExcludedFolders()
    {
        var config = Substitute.For<IConfiguration>();
        config.ProgressUpdateInterval.Returns(5000);
        var handler = new CreateCacheCommandHandler(config, Substitute.For<IMessageBus>(), new OperationCancellation());

        await handler.OnHandle(new CreateCacheCommand(_data) { ExcludePaths = ["node_modules"] }, CancellationToken.None);

        var catalog = Directory.GetFiles(_dir, "*.cdex").Single();
        var children = CatalogTreeBuilder.FromColumnarFiles([catalog])[0].Children.ToDictionary(c => c.Path);
        Assert.That(children.Keys, Is.EquivalentTo(new[] { "kept", "node_modules" }));
        Assert.That(children["kept"].Children.Select(c => c.Path), Is.EquivalentTo(new[] { "a.txt" }));
        Assert.That(children["node_modules"].Children ?? [], Is.Empty);
    }
}
