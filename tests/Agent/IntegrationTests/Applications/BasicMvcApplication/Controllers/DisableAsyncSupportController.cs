using System.Web.Mvc;

namespace BasicMvcApplication.Controllers
{
    public class DisableAsyncSupportController : Controller
    {
        protected override bool DisableAsyncSupport => true;

        public ActionResult Index()
        {
            return View();
        }
    }
}
