using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using NewRelic.Agent.IntegrationTests.Shared.Web;

namespace HttpCollector
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

			GlobalConfiguration.Configuration.Formatters.Insert(0, new StreamMediaTypeFormatter());
		}
    }
}
