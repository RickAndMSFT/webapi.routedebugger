using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http.Controllers;

namespace RouteDebugger.Components
{
    /// <summary>
    /// Inspect action selector wrap the original action selector.
    /// 
    /// It exames the request header. If inspect header is found, it run action selecting
    /// simulator and save the inspect data in request property.
    /// </summary>
    public class InspectActionSelector : IHttpActionSelector
    {
        private IHttpActionSelector _delegating;

        public InspectActionSelector(IHttpActionSelector delegating)
        {
            _delegating = delegating;
        }

        public ILookup<string, HttpActionDescriptor> GetActionMapping(HttpControllerDescriptor controllerDescriptor)
        {
            return _delegating.GetActionMapping(controllerDescriptor);
        }

        public HttpActionDescriptor SelectAction(HttpControllerContext controllerContext)
        {
            var request = controllerContext.Request;
            if (request.IsInspectRequest())
            {
                var simulate = new ActionSelectSimulator();
                request.Properties[RequestHelper.ActionCache] = simulate.Simulate(controllerContext);
            }

            var selectedAction = _delegating.SelectAction(controllerContext);

            return selectedAction;
        }
    }
}