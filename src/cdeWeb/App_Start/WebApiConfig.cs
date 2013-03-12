using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace cdeWeb
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
            DisableXMLMediaType(config);
        }

        private static void DisableXMLMediaType(HttpConfiguration config)
        {
            config.Formatters.XmlFormatter.SupportedMediaTypes.Remove(
                config.Formatters.XmlFormatter.SupportedMediaTypes.FirstOrDefault(t => t.MediaType == "application/xml"));
        }
    }
}
