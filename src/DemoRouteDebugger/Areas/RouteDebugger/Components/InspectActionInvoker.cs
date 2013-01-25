using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;

namespace RouteDebugger.Components
{
    /// <summary>
    /// Inspect action invoker hijack the original invoker. It examines the header before 
    /// execute the action. If the inspect header exists, it stop executing the action but 
    /// return the inspect data in a 200 response.
    /// 
    /// The inspect data saved in request property are collected when the request is passed
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