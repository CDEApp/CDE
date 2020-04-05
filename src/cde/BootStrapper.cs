using Autofac;
using cde.Config;
using cdeLib;
using cdeLib.Infrastructure;
using IConfiguration = cdeLib.Infrastructure.Config.IConfiguration;

namespace cde
{
    /// <summary>
    /// IoC Bootstrapper
    /// </summary>
    public static class BootStrapper
    {
        public static IContainer Components(string[] args)
        {
            var builder = new ContainerBuilder();

            // config
            var config = new ConfigBuilder().Build(args);
            builder.RegisterInstance(config);

            //singletons.
            builder.RegisterType<Logger>().As<ILogger>().SingleInstance();
            builder.RegisterType<Configuration>().As<IConfiguration>().SingleInstance();
            builder.RegisterType<ApplicationDiagnostics>().As<IApplicationDiagnostics>().SingleInstance();

            builder.RegisterType<Duplication>().InstancePerLifetimeScope();

            //autowire properties
            builder.RegisterType<DirEntry>().PropertiesAutowired();


            return builder.Build();
        }
    }
}