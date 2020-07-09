using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Providers.Wrapper.Mvc3
{
	public static class MvcRouteNamingHelper
	{
		[NotNull]
		public static String TryGetControllerNameFromObject([NotNull] ControllerContext controllerContext)
		{
			var controller = controllerContext.Controller;
			if (controller == null)
				return "Unknown Controller";

			var controllerType = controller.GetType();
			return controllerType.Name;
		}

		[NotNull]
		public static String TryGetActionNameFromRouteParameters(MethodCall methodCall, [NotNull] RouteData routeData)
		{
			var actionName = methodCall.MethodArguments.ExtractAs<String>(1);
			if (actionName != null) 
				return actionName;

			var directRouteMatches = routeData.Values.GetValueOrDefault("MS_DirectRouteMatches") as IEnumerable<RouteData> ?? Enumerable.Empty<RouteData>();
			routeData = directRouteMatches.FirstOrDefault() ?? routeData;
			actionName = routeData.Values.GetValueOrDefault("action") as String;

			return actionName ?? "Unknown Action";
		}
	}
}
