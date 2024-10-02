// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Web;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Providers.Wrapper.AspNet.Shared;
using NewRelic.Agent.Extensions.SystemExtensions;

namespace NewRelic.Providers.Wrapper.AspNet.IntegratedPipeline
{
    public class FinishPipelineRequestWrapper : IWrapper
    {
        public const string WrapperName = "AspNet.FinishPipelineRequestTracer";
        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var canWrap = methodInfo.RequestedWrapperName.Equals(WrapperName, StringComparison.OrdinalIgnoreCase);
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (!HttpRuntime.UsingIntegratedPipeline)
                return Delegates.NoOp;

            var httpContext = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<HttpContext>(0);
            HttpContextActions.TransactionShutdown(agent, httpContext);

            var segment = agent.CastAsSegment(httpContext.Items[HttpContextActions.HttpContextSegmentKey] as ISegment);
            httpContext.Items[HttpContextActions.HttpContextSegmentKey] = null;
            httpContext.Items[HttpContextActions.HttpContextSegmentTypeKey] = null;
            segment.End();
            agent.CurrentTransaction.End();

            return Delegates.NoOp;
        }
    }
}
