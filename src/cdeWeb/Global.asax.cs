using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Autofac;
using Autofac.Integration.WebApi;
using Microsoft.AspNet.SignalR;
using cdeLib;
using cdeWeb.App_Start;
using DirEntry = cdeWeb.Models.DirEntry;

namespace cdeWeb
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class WebApiApplication : HttpApplication
    {
        private BackgroundServerTimeTimer bstt;

        // maybe automapper ?

        protected void Application_Start()
        {
            bstt = new BackgroundServerTimeTimer();

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
                .WithParameter("basePath", basePath)
                .SingleInstance();
        }
    }

    public class ServerTimeHub : Hub
    {
        public string GetServerTime()
        {
            return DateTime.UtcNow.ToString();
        }
    }

    public class ClientPushHub : Hub
    {
    }

    public class SearchHub: Hub
    {
        private enum FindState
        {
            StartLoad,
            EndFileLoaded,
            EndLoad,
            StartSearch,
            EndSearch
        };

        private readonly IHubContext _hub;

        public SearchHub()
        {
            _hub = GlobalHost.ConnectionManager.GetHubContext<SearchHub>();
        }

        public int Query(string query, string param)
        {
            Debug.WriteLine(string.Format("Query parameters: \"{0}\" {1}", query, param));
            //hub.Clients.All.filesToLoadFred(23, "drifty...!");
            _hub.Clients.Client(Context.ConnectionId).filesToLoad(27, "drifty...!");
            _hub.Clients.Client(Context.ConnectionId).addDirEntry(
                new DirEntry {
                    Modified = new DateTime(2013,01,02,09,10,11, DateTimeKind.Utc),
                    Name = "Moo",
                    Path = @"D:\Fro\Moo",
                    Size = 321
                });
            return 7;
        }
    }
}