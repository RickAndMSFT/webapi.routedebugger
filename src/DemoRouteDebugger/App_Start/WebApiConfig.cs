using System.Web.Http;
using System.Web.Http.Controllers;
using RouteDebugger.Components;

namespace DemoRouteDebugger
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

            config.Routes.MapHttpRoute(
                name: "Route Two",
                routeTemplate: "api2/{controller}/{action}",
                defaults: new { action = RouteParameter.Optional }
            );

            config.Routes.MapHttpRoute(
                name: "Diagnose",
                routeTemplate: "diagnose/{area}/{level}",
                defaults: new { area = "all", level = "error" });

            config.Routes.MapHttpRoute(
                name: "Handler",
                routeTemplate: "handle",
                defaults: null,
                constraints: null,
                handler: new DemoMessageHandler());

            /// Case 1: http://stackoverflow.com/questions/13876816/web-api-routing
            //config.Routes.MapHttpRoute(
            //    name: "MachineApi",
            //    routeTemplate: "api/machine/{code}/all");

            // Case 2: http://stackoverflow.com/questions/13869541/why-is-my-message-handler-running-even-when-it-is-not-defined-in-webapi
            //config.Routes.MapHttpRoute(
            //    "NoAuthRequiredApi",
            //    "api/auth/",
            //    new { id = RouteParameter.Optional });

            //config.Routes.MapHttpRoute(
            //    "DefaultApi",
            //    "api/{controller}/{id}",
            //    new { id = RouteParameter.Optional },
            //    null,
            //    new DemoMessageHandler());

            /// Case 3: http://stackoverflow.com/questions/14058228/asp-net-web-api-no-action-was-found-on-the-controller
            //config.Routes.MapHttpRoute(
            //    name: "DefaultApi",
            //    routeTemplate: "api/{controller}/{action}/{id}",
            //    defaults: new { action = "get", id = RouteParameter.Optional }
            //);

            /// Case 4: http://stackoverflow.com/questions/13982896/versioning-web-api-actions-in-asp-net-mvc-4/14059654#14059654

        }
    }
}