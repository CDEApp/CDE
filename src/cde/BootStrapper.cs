using Autofac;
using cdeLib.Infrastructure;

namespace cdeLib
{
    /// <summary>
    /// IoC Bootstrapper
    /// </summary>
    public class BootStrapper
    {
        public static IContainer  Components()
        {
            var builder = new ContainerBuilder();

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