using cde.Config;
using cdeLib.Infrastructure;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace cdeLibTest.Infrastructure;

public class ConfigurationTests
{
    [Test]
    public void Can_Read_Configuration_Properties()
    {
        var config = ConfigBuilder.Build([]);
        var configuration = new Configuration(config);
        var sut = configuration.ProgressUpdateInterval;
        ClassicAssert.IsNotNull(sut);
    }
}