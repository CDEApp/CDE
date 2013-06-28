using System.Web.Routing;
using cdeWeb.App_Start;

[assembly: WebActivatorEx.PreApplicationStartMethod(typeof(RegisterHubs), "Start")]

namespace cdeWeb.App_Start
{
    public static class RegisterHubs
    {
        public static void Start()
        {
            // Register the default hubs route: ~/signalr
            RouteTable.Routes.MapHubs();
        }
    }
}
