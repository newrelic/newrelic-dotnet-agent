// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions.Collections;

namespace NewRelic.Providers.Wrapper.HttpWebRequest;

public class GetResponseWrapper : IWrapper
{
    private const string GetResponseMethod = "GetResponse";
    private const string BeginGetResponseMethod = "BeginGetResponse";

    public bool IsTransactionRequired => true;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
    {
        return new CanWrapResponse(methodInfo.RequestedWrapperName == nameof(GetResponseWrapper));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        var httpWebRequest = instrumentedMethodCall.MethodCall.InvocationTarget as System.Net.HttpWebRequest;
        if (httpWebRequest == null)
        {
            throw new NullReferenceException(nameof(httpWebRequest));
        }

        switch (instrumentedMethodCall.MethodCall.Method.MethodName)
        {
            case GetResponseMethod:
                return BeforeGetResponse(instrumentedMethodCall, httpWebRequest, transaction);
            case BeginGetResponseMethod:
                return BeforeBeginGetResponse(instrumentedMethodCall, agent, httpWebRequest, transaction);
            default:
                return BeforeEndGetResponse(httpWebRequest, transaction);
        }
    }

    private static AfterWrappedMethodDelegate BeforeGetResponse(InstrumentedMethodCall instrumentedMethodCall, System.Net.HttpWebRequest httpWebRequest, ITransaction transaction)
    {
        var uri = httpWebRequest.RequestUri;
        if (uri == null)
        {
            return Delegates.NoOp;
        }

        ISegment segment;
        IExternalSegmentData externalSegmentData;

        // On a POST/PUT, GetRequestStreamWrapper already created the external segment and injected
        // the DT headers (on the calling thread, with that segment current). Reuse and end it here.
        // On a bodyless GET there is no pending segment, so create one now.
        if (HttpWebRequestSegmentState.TryTake(httpWebRequest, out var pendingSegment))
        {
            segment = pendingSegment.Segment;
            externalSegmentData = pendingSegment.ExternalSegmentData;
        }
        else
        {
            var method = httpWebRequest.Method ?? "<unknown>";
            segment = HttpWebRequestExternalSegment.Create(transaction, instrumentedMethodCall, uri, method, out externalSegmentData);
        }

        return Delegates.GetDelegateFor<HttpWebResponse>(
            onSuccess: response =>
            {
                TryProcessResponse(response, transaction, segment, externalSegmentData);
                segment.End();
            },
            onFailure: exception =>
            {
                TryProcessResponse((exception as WebException)?.Response, transaction, segment, externalSegmentData);
                segment.End(exception);
            }
        );
    }

    // Async response side of HttpWebRequest. On .NET Framework the TAP method GetResponseAsync is
    // implemented on the base System.Net.WebRequest as Task.Factory.FromAsync(BeginGetResponse,
    // EndGetResponse), so it is not a method on HttpWebRequest and cannot be matched there. The
    // classic APM pair BeginGetResponse/EndGetResponse is what actually runs (both for an explicit
    // Begin/End caller and underneath GetResponseAsync), so that is what we instrument.
    //
    // Two cases reach BeginGetResponse:
    //  1. A body request (POST/PUT) whose external segment was already created and DT-injected on
    //     the request-stream thread by GetRequestStreamWrapper, and handed off through
    //     HttpWebRequestSegmentState. We reuse that segment.
    //  2. A bodyless request (e.g. await GetResponseAsync() on a GET) with no request-stream call,
    //     so no handoff entry exists. Here we create the external segment and inject the DT headers
    //     ourselves, the same way GetRequestStreamWrapper does for the body case.
    //
    // In both cases the request is ours only if no external segment is already current. Other
    // instrumentation that uses HttpWebRequest internally - WCF, HttpClient (whose .NET Framework
    // handler is layered over HttpWebRequest), and RestSharp - creates its own external segment
    // first, so when its internal HttpWebRequest reaches BeginGetResponse that segment is current
    // (IsExternal). We skip those, exactly as GetRequestStreamWrapper does, so we never double
    // create a segment or double-inject headers.
    //
    // The segment (reused or created) is held across the async gap and ended in EndGetResponse,
    // which also processes the inbound response. The transaction is held open between Begin and End
    // so it does not finalize while the request is in flight.
    //
    // Known limitation: if the transaction context does not flow to EndGetResponse (e.g. a caller that
    // blocks on IAsyncResult.AsyncWaitHandle or polls IsCompleted and then calls EndGetResponse on an
    // unrelated thread), the EndGetResponse wrapper is skipped and the Hold is only released when the
    // transaction is finalized. The common shapes - await GetResponseAsync, or an AsyncCallback passed
    // to BeginGetResponse - flow the captured execution context and release normally.
    private static AfterWrappedMethodDelegate BeforeBeginGetResponse(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, System.Net.HttpWebRequest httpWebRequest, ITransaction transaction)
    {
        ISegment segment;
        IExternalSegmentData externalSegmentData;

        if (HttpWebRequestSegmentState.TryTake(httpWebRequest, out var pendingSegment))
        {
            // Body request: reuse the segment handed off from GetRequestStreamWrapper (DT headers
            // were already injected there, on the request-stream thread).
            segment = pendingSegment.Segment;
            externalSegmentData = pendingSegment.ExternalSegmentData;

            // EndGetResponse can run on a different thread (the I/O completion). Move the
            // transaction into async storage now - before BeginGetResponse captures the execution
            // context for its completion callback - so it flows to EndGetResponse.
            transaction.AttachToAsync();
        }
        else
        {
            // No handoff => a bodyless async request. If an external segment is already current,
            // other instrumentation (WCF/HttpClient/RestSharp) owns this call and injects its own
            // headers; do nothing.
            if (transaction.CurrentSegment.IsExternal)
            {
                return Delegates.NoOp;
            }

            var uri = httpWebRequest.RequestUri;
            if (uri == null)
            {
                return Delegates.NoOp;
            }

            // Move into async storage before creating the segment (mirrors GetRequestStreamWrapper's
            // TAP path) so the external segment is current in the async logical context when the
            // headers are injected and so the transaction context flows to EndGetResponse.
            transaction.AttachToAsync();

            var method = httpWebRequest.Method ?? "<unknown>";
            segment = HttpWebRequestExternalSegment.Create(transaction, instrumentedMethodCall, uri, method, out externalSegmentData);

            // Inject directly on this thread, with the external segment current (so the downstream
            // parent is the external client span). SerializeHeaders runs off the async logical
            // context after AttachToAsync and would see a non-external current segment and skip, so
            // the GET would otherwise go out untraced. Headers.Set is idempotent, so a later
            // SerializeHeaders re-injection is harmless.
            HttpWebRequestExternalSegment.InjectDistributedTraceHeaders(transaction, agent, httpWebRequest);
        }

        // Hold the transaction open while the request is in flight, and re-store the segment (keyed
        // by the request instance, the invocation target of both BeginGetResponse and
        // EndGetResponse) so EndGetResponse can take it.
        transaction.Hold();
        HttpWebRequestSegmentState.Set(httpWebRequest, segment, externalSegmentData);

        return Delegates.GetDelegateFor<IAsyncResult>(
            onSuccess: _ =>
            {
                // BeginGetResponse returned; the segment lives until EndGetResponse. Take it off
                // this thread's call stack so it does not parent unrelated work.
                segment.RemoveSegmentFromCallStack();
            },
            onFailure: exception =>
            {
                // BeginGetResponse threw; the request never went out. Undo the handoff and end so
                // we do not leak the held transaction or a stale table entry.
                HttpWebRequestSegmentState.TryTake(httpWebRequest, out _);
                segment.End(exception);
                transaction.Release();
            });
    }

    private static AfterWrappedMethodDelegate BeforeEndGetResponse(System.Net.HttpWebRequest httpWebRequest, ITransaction transaction)
    {
        // No pending segment => BeginGetResponse did not hand one off for this request, so there is
        // nothing to end here.
        if (!HttpWebRequestSegmentState.TryTake(httpWebRequest, out var pendingSegment))
        {
            return Delegates.NoOp;
        }

        var segment = pendingSegment.Segment;
        var externalSegmentData = pendingSegment.ExternalSegmentData;

        return Delegates.GetDelegateFor<WebResponse>(
            onSuccess: response =>
            {
                TryProcessResponse(response, transaction, segment, externalSegmentData);
                segment.End();
                transaction.Release();
            },
            onFailure: exception =>
            {
                TryProcessResponse((exception as WebException)?.Response, transaction, segment, externalSegmentData);
                segment.End(exception);
                transaction.Release();
            });
    }

    private static void TryProcessResponse(WebResponse response, ITransaction transaction, ISegment segment, IExternalSegmentData externalSegmentData)
    {
        if (segment == null)
        {
            return;
        }

        var httpWebResponse = response as HttpWebResponse;
        var statusCode = httpWebResponse?.StatusCode;
        if (statusCode.HasValue)
        {
            externalSegmentData.SetHttpStatus((int)statusCode.Value);
        }

        var headers = response?.Headers?.ToDictionary();
        if (headers == null)
        {
            return;
        }

        transaction.ProcessInboundResponse(headers, segment);
    }
}
