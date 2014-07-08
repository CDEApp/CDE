using System.Reflection;
using System.Web;
using System.Web.Routing;
using Autofac;
using Autofac.Integration.SignalR;
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
            bstt = new BackgroundServerTimeTimer();

            RegisterHubs.Start(RouteTable.Routes);

        }

        private void RegisterContainer(string appDataPath)
        {
            var controllerAssembly = Assembly.GetExecutingAssembly();
            var builder = new ContainerBuilder();
            builder.RegisterHubs(controllerAssembly); // SignalR (todo this doesnt seem to be required)
            builder.RegisterAssemblyTypes(controllerAssembly)
                .Where(t => t.Name.EndsWith("Repository"))
                .AsImplementedInterfaces();

            RegisterCDEData(builder, appDataPath);
            var container = builder.Build();
            var resolverSignalR = new AutofacDependencyResolver(container);
            GlobalHost.DependencyResolver = resolverSignalR; // SignalR resolver
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
