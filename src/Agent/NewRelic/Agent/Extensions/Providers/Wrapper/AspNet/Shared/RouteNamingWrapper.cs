// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Web.Routing;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AspNet.Shared
{
    public class RouteNamingWrapper : IWrapper
    {
        public readonly string WrapperName = "AspNet.GetRouteDataTracer";

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var canWrap = methodInfo.RequestedWrapperName.Equals(WrapperName, StringComparison.OrdinalIgnoreCase);
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
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

                // Avoid overriding lower priority uri name unless we actually have something
                if (String.IsNullOrEmpty(url))
                    return;

                transaction.SetWebTransactionName(WebTransactionType.ASP, url, TransactionNamePriority.Route);
            });
        }
    }
}
