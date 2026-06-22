// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions.Collections;

namespace NewRelic.Providers.Wrapper.HttpWebRequest;

// Async response side of HttpWebRequest. On .NET Framework the TAP method GetResponseAsync is
// implemented on the base System.Net.WebRequest as Task.Factory.FromAsync(BeginGetResponse,
// EndGetResponse), so it is not a method on HttpWebRequest and cannot be matched there. The
// classic APM pair BeginGetResponse/EndGetResponse is what actually runs (both for an explicit
// Begin/End caller and underneath GetResponseAsync), so that is what we instrument.
//
// This wrapper acts ONLY on the external segment handed off by GetRequestStreamWrapper through
// HttpWebRequestSegmentState (a bare POST/PUT/Begin-End body request, where the DT headers were
// already injected on the request-stream thread). The presence of that handoff entry is the
// reliable signal that the request is ours. Other instrumentation that uses HttpWebRequest
// internally - WCF, HttpClient (whose .NET Framework handler is layered over HttpWebRequest), and
// RestSharp - owns its own external segment and never populates the table, so we never double
// create a segment or double-inject headers for it. Inspecting the current segment instead would
// be unreliable here, because the owner's segment is often not the current segment on the thread
// where BeginGetResponse runs across the async boundary.
//
// The handed-off segment is held across the async gap and ended in EndGetResponse, which also
// processes the inbound response. The transaction is held open between Begin and End so it does
// not finalize while the request is in flight.
//
// Known limitation: if the transaction context does not flow to EndGetResponse (e.g. a caller that
// blocks on IAsyncResult.AsyncWaitHandle or polls IsCompleted and then calls EndGetResponse on an
// unrelated thread), the EndGetResponse wrapper is skipped and the Hold is only released when the
// transaction is finalized. The common shapes - await GetResponseAsync, or an AsyncCallback passed
// to BeginGetResponse - flow the captured execution context and release normally.
public class GetResponseAsyncWrapper : IWrapper
{
    private const string BeginMethodName = "BeginGetResponse";
    private const string EndMethodName = "EndGetResponse";
    private static readonly string[] ResponseMethods = { BeginMethodName, EndMethodName };

    public bool IsTransactionRequired => true;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
    {
        var canWrap = methodInfo.Method.MatchesAny(assemblyName: "System", typeName: "System.Net.HttpWebRequest", methodNames: ResponseMethods);
        return new CanWrapResponse(canWrap);
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        var httpWebRequest = instrumentedMethodCall.MethodCall.InvocationTarget as System.Net.HttpWebRequest;
        if (httpWebRequest == null)
        {
            throw new NullReferenceException(nameof(httpWebRequest));
        }

        if (instrumentedMethodCall.MethodCall.Method.MethodName == BeginMethodName)
        {
            return BeforeBeginGetResponse(httpWebRequest, transaction);
        }

        return BeforeEndGetResponse(httpWebRequest, transaction);
    }

    private static AfterWrappedMethodDelegate BeforeBeginGetResponse(System.Net.HttpWebRequest httpWebRequest, ITransaction transaction)
    {
        // Act only on a segment we handed off from the request-stream path. No entry => a bodyless
        // request or a call owned by other instrumentation (WCF/HttpClient/RestSharp); do nothing.
        if (!HttpWebRequestSegmentState.TryTake(httpWebRequest, out var pendingSegment))
        {
            return Delegates.NoOp;
        }

        var segment = pendingSegment.Segment;
        var externalSegmentData = pendingSegment.ExternalSegmentData;

        // EndGetResponse can run on a different thread (the I/O completion). Move the transaction
        // into async storage now - before BeginGetResponse captures the execution context for its
        // completion callback - so it flows to EndGetResponse, and hold it open while the request
        // is in flight. Re-store the segment (keyed by the request instance, the invocation target
        // of both BeginGetResponse and EndGetResponse) so EndGetResponse can take it.
        transaction.AttachToAsync();
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
