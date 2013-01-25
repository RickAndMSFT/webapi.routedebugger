using System;
using System.Web.Http;

namespace DemoRouteDebugger.Controllers
{
    public class SecondController : ApiController
    {
        [HttpPut]
        public void ActionOne(int value)
        {
        }

        [HttpPatch]
        [HttpHead]
        public Guid ActionTwo()
        {
            return Guid.NewGuid();
        }

        public void Post(int data)
        {
        }
    }
}