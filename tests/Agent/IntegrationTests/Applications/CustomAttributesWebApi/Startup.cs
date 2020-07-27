using System.Web.Http;
using Owin;

namespace NewRelic.Agent.IntegrationTests.Applications.CustomAttributesWebApi
{
    public class Startup
    {
        // This code configures Web API. The Startup class is specified as a type parameter in the WebApp.Start method.
        public void Configuration(IAppBuilder appBuilder)
        {
            var config = new HttpConfiguration();
            config.MapHttpAttributeRoutes();
            appBuilder.UseWebApi(config);
        }
    }
}
