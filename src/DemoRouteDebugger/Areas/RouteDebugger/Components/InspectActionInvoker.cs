using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;

namespace RouteDebugger.Components
{
    /// <summary>
    /// Hijacks the original invoker. It examines the header before 
    /// executing the action. If the inspect header exists, returns the inspection data in a 200 response.
    /// If the inspection header does not exist, the delegate calls the default InvokeActionAsync method.
    /// 
    /// The inspection data saved in the request property are collected when the request is passed
    /// along the stack.
    /// </summary>
    public class InspectActionInvoker : IHttpActionInvoker
    {
        private IHttpActionInvoker _delegating;

        public InspectActionInvoker(IHttpActionInvoker delegating)
        {
            _delegating = delegating;
        }

        public Task<HttpResponseMessage> InvokeActionAsync(HttpActionContext actionContext, CancellationToken cancellationToken)
        {
            if (actionContext.Request.IsInspectRequest())
            {
                var inspectData = new InspectData(actionContext.Request);
                inspectData.RealHttpStatus = HttpStatusCode.OK;
                return Task.FromResult<HttpResponseMessage>(actionContext.Request.CreateResponse<InspectData>(
                    HttpStatusCode.OK, inspectData));
            }
            else
            {
                return _delegating.InvokeActionAsync(actionContext, cancellationToken);
            }
        }
    }
}