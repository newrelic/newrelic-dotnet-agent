using System.Web.Http;

public static class WebApiConfig
{
	public static void Register(HttpConfiguration config)
	{
		// Web API configuration and services

		// Web API routes
		config.MapHttpAttributeRoutes();

		config.Routes.MapHttpRoute(
			name: "RestAPI",
			routeTemplate: "api/{controller}/{id}",
			defaults: new { id = RouteParameter.Optional }
		);
	}
}
