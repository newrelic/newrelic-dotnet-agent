// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.HttpWebRequest;

// Shared helpers for the HttpWebRequest wrappers. The request-stream path (GetRequestStreamWrapper)
// and the response path (GetResponseWrapper) create the external segment and inject the
// distributed-trace headers the same way, so that logic lives here once.
internal static class HttpWebRequestExternalSegment
{
    // Non-capturing, so the compiler caches a single delegate instance - no per-call allocation.
    private static readonly Action<System.Net.HttpWebRequest, string, string> _setHeaders =
        (carrier, key, value) => carrier.Headers?.Set(key, value);

    public static ISegment Create(ITransaction transaction, InstrumentedMethodCall instrumentedMethodCall, Uri uri, string method, out IExternalSegmentData externalSegmentData)
    {
        var transactionExperimental = transaction.GetExperimentalApi();
        externalSegmentData = transactionExperimental.CreateExternalSegmentData(uri, method);
        var segment = transactionExperimental.StartSegment(instrumentedMethodCall.MethodCall);
        segment.GetExperimentalApi().SetSegmentData(externalSegmentData);
        segment.MakeCombinable();
        return segment;
    }

    public static void InjectDistributedTraceHeaders(ITransaction transaction, IAgent agent, System.Net.HttpWebRequest httpWebRequest)
    {
        try
        {
            transaction.InsertDistributedTraceHeaders(httpWebRequest, _setHeaders);
        }
        catch (Exception ex)
        {
            agent.HandleWrapperException(ex);
        }
    }
}
