// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.HttpWebRequest;

// On a request that sends a body (POST/PUT), HttpWebRequest serializes the request headers when
// the application obtains the request stream - before GetResponse() runs. Without instrumentation
// here, no external segment exists yet when SerializeHeaders fires, so SerializeHeadersWrapper
// skips distributed-trace header injection and the request goes out untraced.
//
// Creating the external segment here makes it the current segment before the headers are
// serialized, so SerializeHeaders injects them with the external client span as the parent (the
// same linkage the GET path produces). The segment is handed off to GetResponseWrapper (via
// HttpWebRequestSegmentState) so a single external segment spans the whole request/response.
//
// All three ways of obtaining the request stream are covered: the synchronous GetRequestStream,
// the APM-pattern BeginGetRequestStream, and the TAP-pattern GetRequestStreamAsync. On .NET
// Framework GetRequestStreamAsync is layered over BeginGetRequestStream, so a call may match more
// than one of these; the Contains() guard makes that harmless by creating the segment only once.
//
// When an external segment is already the current segment, another instrumentation (WCF,
// HttpClient, RestSharp, ...) created it and is using HttpWebRequest internally; that
// instrumentation owns the segment and header injection, so this wrapper does nothing.
public class GetRequestStreamWrapper : IWrapper
{
    public bool IsTransactionRequired => true;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
    {
        return new CanWrapResponse(methodInfo.RequestedWrapperName == nameof(GetRequestStreamWrapper));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        var httpWebRequest = instrumentedMethodCall.MethodCall.InvocationTarget as System.Net.HttpWebRequest;
        if (httpWebRequest == null)
        {
            throw new NullReferenceException(nameof(httpWebRequest));
        }

        var uri = httpWebRequest.RequestUri;
        if (uri == null)
        {
            return Delegates.NoOp;
        }

        // An external segment is already current => WCF/HttpClient/etc. owns this call and will
        // create/end its own segment and inject the headers. Do nothing here.
        if (transaction.CurrentSegment.IsExternal)
        {
            return Delegates.NoOp;
        }

        // The request stream can be obtained more than once for the same request (and the sync,
        // APM, and TAP forms can layer on one another); only create the segment the first time.
        if (HttpWebRequestSegmentState.Contains(httpWebRequest))
        {
            return Delegates.NoOp;
        }

        if (instrumentedMethodCall.IsAsync)
        {
            transaction.AttachToAsync();
        }

        var method = httpWebRequest.Method ?? "<unknown>";

        var transactionExperimental = transaction.GetExperimentalApi();
        var externalSegmentData = transactionExperimental.CreateExternalSegmentData(uri, method);
        var segment = transactionExperimental.StartSegment(instrumentedMethodCall.MethodCall);
        segment.GetExperimentalApi().SetSegmentData(externalSegmentData);
        segment.MakeCombinable();

        // Inject the distributed-trace headers here, on the calling thread, with the external
        // segment current (so the downstream parent is the external client span). We cannot rely on
        // SerializeHeadersWrapper for this: on the TAP path (GetRequestStreamAsync) AttachToAsync
        // moves the external segment into AsyncLocal, and the underlying header serialization runs
        // off this logical context, so SerializeHeaders sees a non-external current segment and
        // skips injection - the POST/PUT would go out untraced. Headers.Set is idempotent, so a
        // later SerializeHeaders re-injection (sync/APM paths) is harmless.
        var setHeaders = new Action<System.Net.HttpWebRequest, string, string>((carrier, key, value) =>
        {
            carrier.Headers?.Set(key, value);
        });

        try
        {
            transaction.InsertDistributedTraceHeaders(httpWebRequest, setHeaders);
        }
        catch (Exception ex)
        {
            agent.HandleWrapperException(ex);
        }

        // The external call is not complete until the response is obtained, so do not end the
        // segment here - hand it off to GetResponseWrapper, which processes the response and ends it.
        HttpWebRequestSegmentState.Set(httpWebRequest, segment, externalSegmentData);

        return Delegates.NoOp;
    }
}
