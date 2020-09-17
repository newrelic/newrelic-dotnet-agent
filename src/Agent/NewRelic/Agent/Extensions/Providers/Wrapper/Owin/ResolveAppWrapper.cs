// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Owin
{
    public class ResolveAppWrapper : IWrapper
    {
        public bool IsTransactionRequired => false;
        private static int _isNewRelicMiddlewareAdded;
        private Func<object, object> _getBuilder;
        public Func<object, object> GetBuilder => _getBuilder ?? (_getBuilder = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("Microsoft.Owin.Hosting",
                "Microsoft.Owin.Hosting.Engine.StartContext", "Builder"));

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse("ResolveAppWrapper".Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (0 == Interlocked.CompareExchange(ref _isNewRelicMiddlewareAdded, 1, 0))
            {
                var context = instrumentedMethodCall.MethodCall.MethodArguments[0];

                var app = GetBuilder(context);

                var method = app.GetType().GetMethod("Use");

                method.Invoke(app, new object[]
                {
                typeof(OwinStartupMiddleware), new object[] { agent }
                });

            }

            return Delegates.NoOp;
        }
    }
}
