using System.Linq;
using System.Web.Http;
using System.Web.Http.OData.Builder;
using cdeWeb.Models;

namespace cdeWeb.App_Start
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

        // references
        // http://www.asp.net/web-api/overview/odata-support-in-aspnet-web-api/supporting-odata-query-options
        // http://blogs.msdn.com/b/webdev/archive/2013/01/29/getting-started-with-asp-net-webapi-odata-in-3-simple-steps.aspx
        public static void RegisterOData(HttpConfiguration config)
        {
            config.EnableQuerySupport(); // enable OData

            //
            // todo Add my own result for query.. ?
            //  return   class with   metrics field and EntitySet<DirEntry>
            //

            var modelBuilder = new ODataConventionModelBuilder();
            modelBuilder.EntitySet<DirEntry>("DirEntries");

            //var myQuery = modelBuilder.Entity<DirEntry>().Action("q");
            //myQuery.Parameter<string>("query");
            //myQuery.Returns<SearchResult>();

            var model = modelBuilder.GetEdmModel();

            config.Routes.MapODataRoute(
                routeName: "OData",
                routePrefix: "odata",
                model: model);
        }
    }
}
