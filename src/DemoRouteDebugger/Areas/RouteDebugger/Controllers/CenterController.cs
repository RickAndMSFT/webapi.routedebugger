using System.Linq;
using System.Web.Http;
using System.Web.Mvc;

namespace DemoRouteDebugger.Areas.RouteDebugger.Controllers
{
    public class CenterController : Controller
    {
        public ActionResult Simulate()
        {
            return View();
        }

        public ActionResult Help()
        {
            return View();
        }
    }
}