using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace RejitMvcApplication.Controllers
{
    /// <summary>
    /// Simple endpoint to get the agent spun up.
    /// Specifically outside the CustomInstrumentationController to make it easier to see the real data.
    /// </summary>
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return Content("It am working", "text/plain");
        }
    }
}
