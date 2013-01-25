using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;

namespace RouteDebugger.Components
{
    /// <summary>
    /// Inspect controller selector wrap the original controller selector.
    /// 
    /// It exames the request header. If inspect header is found:
    /// 1. It saves all candiate controllers in inspect data
    /// 2. It maked the selected controller
    /// </summary>
    public class InspectControllerSelector : IHttpControllerSelector
    {
        private IHttpControllerSelector _delegating;

        public InspectControllerSelector(IHttpControllerSelector delegating)
        {
            _delegating = delegating;
        }

        public IDictionary<string, HttpControllerDescriptor> GetControllerMapping()
        {
            return _delegating.GetControllerMapping();
        }

        public HttpControllerDescriptor SelectController(HttpRequestMessage request)
        {
            if (request.IsInspectRequest())
            {
                var controllers = _delegating.GetControllerMapping().Values.Select(desc =>
                    new
                    {
                        desc.ControllerName,
                        desc.ControllerType,
                    });

                request.Properties[RequestHelper.ControllerCache] = controllers;
            }

            var selectedController = _delegating.SelectController(request);

            // if exceptino is not thrown
            request.Properties[RequestHelper.SelectedController] = selectedController.ControllerName;

            return selectedController;
        }
    }
}