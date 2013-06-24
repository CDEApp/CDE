using System.Reflection;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Autofac;
using Autofac.Integration.WebApi;
using cdeWeb.App_Start;

namespace cdeWeb
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class WebApiApplication : HttpApplication
    {
        // maybe automapper ?

        protected void Application_Start()
        {
            var appDataPath = Server.MapPath("~/App_Data");
            RegisterContainer(appDataPath);

            AreaRegistration.RegisterAllAreas();

            WebApiConfig.Register(GlobalConfiguration.Configuration);
            WebApiConfig.RegisterOData(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

        }

        private void RegisterContainer(string appDataPath)
        {
            var builder = new ContainerBuilder();

            var controllerAssembly = Assembly.GetExecutingAssembly();
            builder.RegisterApiControllers(controllerAssembly);

            builder.RegisterAssemblyTypes(controllerAssembly)
                .Where(t => t.Name.EndsWith("Repository"))
                .AsImplementedInterfaces();

            //builder.RegisterWebApiFilterProvider(GlobalConfiguration.Configuration);
            
            RegisterCDEData(builder, appDataPath);

            var container = builder.Build();
            var resolver = new AutofacWebApiDependencyResolver(container);
            GlobalConfiguration.Configuration.DependencyResolver = resolver; // Web API resolver
        }

        private void RegisterCDEData(ContainerBuilder builder, string basePath)
        {
            builder.RegisterType<DataStore>()
                .As<IDataStore>()
                .WithParameter("basePath", basePath);
        }

    }


}