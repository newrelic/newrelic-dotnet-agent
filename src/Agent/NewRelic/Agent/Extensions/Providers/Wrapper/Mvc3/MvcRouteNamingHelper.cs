// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Providers.Wrapper.Mvc3
{
    public static class MvcRouteNamingHelper
    {
        public static string TryGetControllerNameFromObject(ControllerContext controllerContext)
        {
            var controller = controllerContext.Controller;
            if (controller == null)
                return "Unknown Controller";

            var controllerType = controller.GetType();
            return controllerType.Name;
        }
        public static string TryGetActionNameFromRouteParameters(MethodCall methodCall, RouteData routeData)
        {
            var actionName = methodCall.MethodArguments.ExtractAs<string>(1);
            if (actionName != null)
                return actionName;

            var directRouteMatches = routeData.Values.GetValueOrDefault("MS_DirectRouteMatches") as IEnumerable<RouteData> ?? Enumerable.Empty<RouteData>();
            routeData = directRouteMatches.FirstOrDefault() ?? routeData;
            actionName = routeData.Values.GetValueOrDefault("action") as string;

            return actionName ?? "Unknown Action";
        }
    }
}
