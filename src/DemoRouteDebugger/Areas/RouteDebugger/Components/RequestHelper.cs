using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace RouteDebugger.Components
{
    public static class RequestHelper
    {
        public static readonly string InspectHeaderName = "RouteInspecting";
        public static readonly string RouteDataCache = "RD_ROUTEDATA";
        public static readonly string RoutesCache = "RD_ROUTES";
        public static readonly string ControllerCache = "RD_CONTROLLER";
        public static readonly string ActionCache = "RD_ACTION";
        public static readonly string SelectedController = "RD_SELECTED_CONTROLLER";

        public static bool IsInspectRequest(this HttpRequestMessage self)
        {
            IEnumerable<string> values;
            return self.Headers.TryGetValues(InspectHeaderName, out values) && (values.Contains("true"));
        }
    }

    public class InspectData
    {
        public InspectData(HttpRequestMessage request)
        {
            if (request.Properties.ContainsKey(RequestHelper.ActionCache))
            {
                Action = request.Properties[RequestHelper.ActionCache];
            }

            if (request.Properties.ContainsKey(RequestHelper.ControllerCache))
            {
                Controller = request.Properties[RequestHelper.ControllerCache];
            }

            if (request.Properties.ContainsKey(RequestHelper.RoutesCache))
            {
                Routes = request.Properties[RequestHelper.RoutesCache];
            }

            if (request.Properties.ContainsKey(RequestHelper.RouteDataCache))
            {
                RouteData = request.Properties[RequestHelper.RouteDataCache];
            }

            if (request.Properties.ContainsKey(RequestHelper.SelectedController))
            {
                SelectedController = request.Properties[RequestHelper.SelectedController] as string;
            }
        }

        public dynamic Action { get; set; }

        public dynamic Controller { get; set; }

        public dynamic Routes { get; set; }

        public dynamic RouteData { get; set; }

        public HttpStatusCode RealHttpStatus { get; set; }

        public string SelectedController { get; set; }
    }
}