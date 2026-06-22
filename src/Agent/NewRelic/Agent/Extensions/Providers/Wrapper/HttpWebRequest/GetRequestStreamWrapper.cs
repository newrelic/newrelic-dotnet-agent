// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.HttpWebRequest;

// On a request that sends a body (POST/PUT), HttpWebRequest serializes the request headers
// during GetRequestStream() - before GetResponse() creates the external segment. At that point
// SerializeHeadersWrapper skips injection (no external segment is current), so the distributed-
// trace headers never go out and the callee records the request with no caller identity.
//
// Injecting the headers here - before the underlying GetRequestStream serializes them - fixes
// that. GetResponse still creates the external segment for timing/metrics, exactly as before.
//
// When an external segment is already the current segment, another instrumentation (WCF,
// HttpClient, RestSharp, ...) created it and is using HttpWebRequest internally; that
// instrumentation's SerializeHeaders path already injects the headers, so this wrapper does
// nothing to avoid double-injecting.
public class GetRequestStreamWrapper : IWrapper
{
    public bool IsTransactionRequired => true;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
    {
        var canWrap = methodInfo.Method.MatchesAny(assemblyName: "System", typeName: "System.Net.HttpWebRequest", methodName: "GetRequestStream");
        return new CanWrapResponse(canWrap);
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        var request = instrumentedMethodCall.MethodCall.InvocationTarget as System.Net.HttpWebRequest;
        if (request == null)
        {
            throw new NullReferenceException(nameof(request));
        }

        if (request.Headers == null)
        {
            throw new NullReferenceException("request.Headers");
        }

        // An external segment is already current => WCF/HttpClient/etc. owns this call and will
        // inject the headers via SerializeHeadersWrapper. Injecting here too would double-inject.
        if (transaction.CurrentSegment.IsExternal)
        {
            return Delegates.NoOp;
        }

        var setHeaders = new Action<System.Net.HttpWebRequest, string, string>((carrier, key, value) =>
        {
            carrier.Headers?.Set(key, value);
        });

        try
        {
            transaction.InsertDistributedTraceHeaders(request, setHeaders);
        }
        catch (Exception ex)
        {
            agent.HandleWrapperException(ex);
        }

        return Delegates.NoOp;
    }
}
