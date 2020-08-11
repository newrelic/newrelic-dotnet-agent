// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Web;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
    public class FilterWrapper : IWrapper
    {
        public const string WrapperName = "Asp35.FilterTracer";

        private const string BrowerAgentInjectedKey = "NewRelic.BrowerAgentInjected";

        public bool IsTransactionRequired => false;

        private static class Statics
        {
            public static readonly Func<HttpWriter, bool> IgnoringFurtherWrites = VisibilityBypasser.Instance.GeneratePropertyAccessor<HttpWriter, bool>("IgnoringFurtherWrites");
        }

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var canWrap = methodInfo.RequestedWrapperName.Equals(WrapperName, StringComparison.OrdinalIgnoreCase);
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var httpContext = HttpContext.Current;
            // we have seen httpContext == null in the wild, so don't throw an exception
            if (httpContext == null)
                return Delegates.NoOp;

            //have we already added our filter?  If so, we are done.
            if (httpContext.Items.Contains(BrowerAgentInjectedKey))
                return Delegates.NoOp;

            if (httpContext.Response.StatusCode >= 300)
                return Delegates.NoOp;

            var httpWriter = (HttpWriter)instrumentedMethodCall.MethodCall.InvocationTarget;
            if (httpWriter == null)
                throw new NullReferenceException("httpWriter");

            if (Statics.IgnoringFurtherWrites(httpWriter))
                return Delegates.NoOp;

            //add our filter and add a key to httpContext.Items to reflect this. 
            //   (the key is used above to insure we only add our filter once).
            var newFilter = TryGetStreamInjector(agent, httpContext);
            if (newFilter != null)
            {
                httpContext.Response.Filter = newFilter;
                httpContext.Items[BrowerAgentInjectedKey] = true;
            }

            return Delegates.NoOp;
        }

        private static Stream TryGetStreamInjector(IAgent agent, HttpContext httpContext)
        {
            var currentFilter = httpContext.Response.Filter;
            var contentEncoding = httpContext.Response.ContentEncoding;
            var contentType = httpContext.Response.ContentType;
            var requestPath = httpContext.Request.Path;

            // NOTE: We need to be very careful if we decide to move where TryGetStreamInjector is called from. The agent assumes that this call will happen fairly late in the pipeline as it has a side-effect of freezing the transaction name and capturing all of the currently recorded transaction attributes.
            return agent.TryGetStreamInjector(currentFilter, contentEncoding, contentType, requestPath);
        }
    }
}
