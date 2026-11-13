// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using Microsoft.AspNetCore.Http;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.AspNetCore6Plus;

public class ResponseCompressionBodyOnWriteWrapper : IWrapper
{
    private static class LazyAccessors
    {
        public static readonly Func<object, Stream> CompressionStreamFieldGetter =
            VisibilityBypasser.Instance.GenerateFieldReadAccessor<Stream>("Microsoft.AspNetCore.ResponseCompression", "Microsoft.AspNetCore.ResponseCompression.ResponseCompressionBody",
                "_compressionStream");

        public static readonly Action<object, Stream> CompressionStreamFieldSetter =
            VisibilityBypasser.Instance.GenerateFieldWriteAccessor<Stream>("Microsoft.AspNetCore.ResponseCompression", "Microsoft.AspNetCore.ResponseCompression.ResponseCompressionBody",
                "_compressionStream");

        public static readonly Func<object, HttpContext> ContextFieldGetter =
            VisibilityBypasser.Instance.GenerateFieldReadAccessor<HttpContext>("Microsoft.AspNetCore.ResponseCompression", "Microsoft.AspNetCore.ResponseCompression.ResponseCompressionBody",
                "_context");
    }

    public bool IsTransactionRequired => false;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        return new CanWrapResponse("ResponseCompressionBodyOnWriteWrapper".Equals(instrumentedMethodInfo.RequestedWrapperName));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        return Delegates.GetDelegateFor(onSuccess: () =>
        {
            var context = LazyAccessors.ContextFieldGetter.Invoke(instrumentedMethodCall.MethodCall.InvocationTarget);

            // only wrap the compression stream if browser injection is enabled and the request is not a gRPC request.
            if (context != null && agent.Configuration.BrowserMonitoringAutoInstrument && agent.Configuration.EnableAspNetCore6PlusBrowserInjection && context.Request?.ContentType?.ToLower() != "application/grpc")
            {
                // Wrap _compressionStream and replace the current value with our wrapped version
                // check whether we've already wrapped the stream so we don't do it twice
                var currentCompressionStream = LazyAccessors.CompressionStreamFieldGetter.Invoke(instrumentedMethodCall.MethodCall.InvocationTarget);

                if (currentCompressionStream != null && currentCompressionStream.GetType() != typeof(BrowserInjectingStreamWrapper))
                {

                    var responseWrapper = new BrowserInjectingStreamWrapper(agent, currentCompressionStream, context);

                    LazyAccessors.CompressionStreamFieldSetter.Invoke(instrumentedMethodCall.MethodCall.InvocationTarget, responseWrapper);
                }
            }
        });
    }
}