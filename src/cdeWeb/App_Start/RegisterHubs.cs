using System.Web.Routing;
using Microsoft.AspNet.SignalR;

namespace cdeWeb.App_Start
{
    public static class RegisterHubs
    {
        public static void Start(RouteCollection routes)
        {
            // Register the default hubs route: ~/signalr
            routes.MapHubs(new HubConfiguration() { EnableJavaScriptProxies = false });
        }
    }
}
