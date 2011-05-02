using cdeLib.Infrastructure;
using NUnit.Framework;

namespace cdeLibTest.Infrastructure
{
    public class ConfigurationTests
    {
        [Test]
        public void Can_Read_Configuration_Properties()
        {
            IConfiguration configuration = new Configuration();
            var sut = configuration.ProgressUpdateInterval;
            Assert.IsNotNull(sut);
        }
    }
}