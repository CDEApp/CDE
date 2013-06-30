using System.Web.Routing;
using Microsoft.AspNet.SignalR;
using cdeWeb.App_Start;

[assembly: WebActivatorEx.PreApplicationStartMethod(typeof(RegisterHubs), "Start")]

namespace cdeWeb.App_Start
{
    public static class RegisterHubs
    {
        public static void Start()
        {
            // Register the default hubs route: ~/signalr
            //RouteTable.Routes.MapHubs();
            RouteTable.Routes.MapHubs(new HubConfiguration() { EnableJavaScriptProxies = false });
        }
    }
}
