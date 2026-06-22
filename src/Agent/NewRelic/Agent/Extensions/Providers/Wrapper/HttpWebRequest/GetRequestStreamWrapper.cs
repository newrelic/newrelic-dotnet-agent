// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.HttpWebRequest;

// On a request that sends a body (POST/PUT), HttpWebRequest serializes the request headers
// when the application obtains the request stream - before GetResponse() creates the external
// segment. At that point SerializeHeadersWrapper skips injection (no external segment is
// current), so the distributed-trace headers never go out and the callee records the request
// with no caller identity.
//
// Injecting the headers here - before the underlying call serializes them - fixes that.
// GetResponse still creates the external segment for timing/metrics, exactly as before.
//
// All three ways of obtaining the request stream are covered: the synchronous GetRequestStream,
// the APM-pattern BeginGetRequestStream, and the TAP-pattern GetRequestStreamAsync. On .NET
// Framework GetRequestStreamAsync is layered over BeginGetRequestStream, so a call may match
// more than one of these; re-injecting is harmless because the headers are written with Set
// (overwrite), not Add. The injection runs synchronously in BeforeWrappedMethod on the calling
// thread, inside the transaction, before any async work begins.
//
// When an external segment is already the current segment, another instrumentation (WCF,
// HttpClient, RestSharp, ...) created it and is using HttpWebRequest internally; that
// instrumentation's SerializeHeaders path already injects the headers, so this wrapper does
// nothing to avoid double-injecting.
public class GetRequestStreamWrapper : IWrapper
{
    private static readonly string[] RequestStreamMethods = { "GetRequestStream", "BeginGetRequestStream", "GetRequestStreamAsync" };

    public bool IsTransactionRequired => true;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
    {
        var canWrap = methodInfo.Method.MatchesAny(assemblyName: "System", typeName: "System.Net.HttpWebRequest", methodNames: RequestStreamMethods);
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
