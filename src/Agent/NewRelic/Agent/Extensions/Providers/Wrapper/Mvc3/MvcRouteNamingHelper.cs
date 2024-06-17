// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using System.Web.Routing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions;

namespace NewRelic.Providers.Wrapper.Mvc3
{
    public static class MvcRouteNamingHelper
    {
        public static string TryGetControllerNameFromObject(dynamic controllerContext)
        {
            var controller = controllerContext.Controller;
            if (controller == null)
            {
                return "Unknown Controller";
            }

            var controllerType = controller.GetType();
            return controllerType.Name;
        }

        public static string TryGetControllerFullNameFromObject(dynamic controllerContext)
        {
            var controller = controllerContext.Controller;
            var controllerType = controller?.GetType();
            return controllerType?.FullName;
        }

        public static string TryGetActionNameFromRouteParameters(MethodCall methodCall, dynamic routeData)
        {
            var actionName = methodCall.MethodArguments.ExtractAs<string>(1);
            if (actionName != null)
            {
                return actionName;
            }

            var directRouteMatches = routeData?.Values?["MS_DirectRouteMatches"] as IEnumerable<RouteData> ?? Enumerable.Empty<RouteData>();
            routeData = directRouteMatches?.FirstOrDefault() ?? routeData;
            actionName = routeData?.Values?["action"];

            return actionName ?? "Unknown Action";
        }
    }
}
