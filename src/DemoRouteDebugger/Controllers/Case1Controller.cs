using System.Web.Http;

namespace DemoRouteDebugger.Controllers
{
    public class Case1Controller : ApiController
    {
        public int GetInt()
        {
            return 0;
        }

        public string GetString()
        {
            return "sample";
        }
    }
}
