// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Web.Routing;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
    public class RouteNamingWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "System.Web", typeName: "System.Web.Routing.RouteCollection", methodName: "GetRouteData");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            return Delegates.GetDelegateFor<RouteData>(onSuccess: routeData =>
            {
                if (routeData == null)
                    return;

                if (routeData.RouteHandler == null)
                    return;

                if (routeData.RouteHandler is StopRoutingHandler)
                    return;

                var route = routeData.Route as Route;
                if (route == null)
                    return;

                var url = route.Url;
                if (url == null)
                    return;

                transaction.SetWebTransactionName(WebTransactionType.ASP, url, 4);
            });
        }
    }
}
