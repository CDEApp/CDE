using Autofac;
using cdeLib.Catalog;
using cdeLib.Duplicates;
using cdeLib.Entities;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using MediatR.Extensions.Autofac.DependencyInjection;

namespace cdeLib.Module
{
    public class CdelibModule : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // singletons.
            builder.RegisterType<Configuration>().As<IConfiguration>().SingleInstance();
            builder.RegisterType<Logger>().As<ILogger>();
            builder.RegisterType<FindService>().As<IFindService>();
            builder.RegisterType<CatalogRepository>().As<ICatalogRepository>();
            builder.RegisterType<ApplicationDiagnostics>().As<IApplicationDiagnostics>().SingleInstance();

            builder.RegisterType<Duplication>().InstancePerLifetimeScope();

            // auto-wire properties
            builder.RegisterType<DirEntry>().PropertiesAutowired();

            // this will add all your Request- and NotificationHandler
            // that are located in the same project as your program-class
            builder.RegisterMediatR(typeof(CdelibModule).Assembly);
        }
    }
}