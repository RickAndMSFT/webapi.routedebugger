using System.Web.Http;
using System.Web.Mvc;
using RouteDebugger;

namespace DemoRouteDebugger.Areas.RouteDebugger
{
    public class RouteDebuggerAreaRegistration : AreaRegistration
    {
        public override string AreaName
        {
            get
            {
                return "RouteDebugger";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "RouteDebugger_default",
                "RouteDebugger/{action}",
                new { controller = "Center", action = "Simulate" }
            );

            // Replace some of the default routing implementations with our custom debug
            // implementations.
            RouteDebuggerConfig.Register(GlobalConfiguration.Configuration);
        }
    }
}