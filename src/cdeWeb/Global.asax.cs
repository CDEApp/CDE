using System.Reflection;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Autofac;
using Autofac.Integration.SignalR;
using Autofac.Integration.WebApi;
using Microsoft.AspNet.SignalR;
using cdeWeb.App_Start;

namespace cdeWeb
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class WebApiApplication : HttpApplication
    {
        private BackgroundServerTimeTimer bstt;

        // todo maybe automapper ?

        protected void Application_Start()
        {
            var appDataPath = Server.MapPath("~/App_Data");
            RegisterContainer(appDataPath);
            //bstt = new BackgroundServerTimeTimer();

            RegisterHubs.Start(RouteTable.Routes);

            AreaRegistration.RegisterAllAreas();

            WebApiConfig.Register(GlobalConfiguration.Configuration);
            WebApiConfig.RegisterOData(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

        }

        private void RegisterContainer(string appDataPath)
        {
            var controllerAssembly = Assembly.GetExecutingAssembly();
            var builder = new ContainerBuilder();
            builder.RegisterHubs(controllerAssembly); // SignalR (todo this doesnt seem to be required)
            builder.RegisterApiControllers(controllerAssembly); // MVC API
            builder.RegisterAssemblyTypes(controllerAssembly)
                .Where(t => t.Name.EndsWith("Repository"))
                .AsImplementedInterfaces();

            //builder.RegisterWebApiFilterProvider(GlobalConfiguration.Configuration);
            RegisterCDEData(builder, appDataPath);

            var container = builder.Build();

            var resolverSignalR = new AutofacDependencyResolver(container);
            GlobalHost.DependencyResolver = resolverSignalR; // SignalR resolver

            var resolver = new AutofacWebApiDependencyResolver(container);
            GlobalConfiguration.Configuration.DependencyResolver = resolver; // Web API resolver
        }

        private void RegisterCDEData(ContainerBuilder builder, string basePath)
        {
            builder.RegisterType<DataStore>()
                .As<IDataStore>()
                .WithParameter("basePath", basePath)
                .SingleInstance();
        }
    }

};
