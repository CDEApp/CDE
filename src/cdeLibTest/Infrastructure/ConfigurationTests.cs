using cde.Config;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using NUnit.Framework;

namespace cdeLibTest.Infrastructure;

public class ConfigurationTests
{
    [Test]
    public void Can_Read_Configuration_Properties()
    {
        var configurationBuilder = new ConfigBuilder();
        var config = configurationBuilder.Build(System.Array.Empty<string>());
        IConfiguration configuration = new Configuration(config);
        var sut = configuration.ProgressUpdateInterval;
        Assert.IsNotNull(sut);
    }
}