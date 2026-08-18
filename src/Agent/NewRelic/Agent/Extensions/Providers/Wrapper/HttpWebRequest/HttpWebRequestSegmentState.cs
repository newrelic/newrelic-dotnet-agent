// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;

namespace NewRelic.Providers.Wrapper.HttpWebRequest;

// Hands the external segment created for an HttpWebRequest between the wrappers that span its
// lifecycle, so a single external segment covers the whole request/response. Two flows use it:
// GetRequestStreamWrapper -> GetResponseWrapper for body (POST/PUT) requests, and within
// GetResponseWrapper from BeginGetResponse -> EndGetResponse for async requests. The wrapper that
// creates the segment injects the distributed-trace headers directly, on the calling thread with
// the external segment current, so the downstream parent is the external client span.
//
// Keyed weakly by the HttpWebRequest instance: parallel requests use distinct instances, so they
// never collide, and an abandoned request (one where the response is never obtained) cannot leak
// a table entry. Set/TryTake are serialized so a request-stream call racing a response call on the
// same instance cannot double-create or double-end.
internal static class HttpWebRequestSegmentState
{
    internal sealed class PendingSegment
    {
        public ISegment Segment;
        public IExternalSegmentData ExternalSegmentData;
    }

    private static readonly object _lock = new object();

    private static readonly ConditionalWeakTable<System.Net.HttpWebRequest, PendingSegment> _pending =
        new ConditionalWeakTable<System.Net.HttpWebRequest, PendingSegment>();

    public static bool Contains(System.Net.HttpWebRequest request)
    {
        lock (_lock)
        {
            return _pending.TryGetValue(request, out _);
        }
    }

    public static void Set(System.Net.HttpWebRequest request, ISegment segment, IExternalSegmentData externalSegmentData)
    {
        lock (_lock)
        {
            // Drop any stale entry first (e.g., the request stream was obtained more than once).
            _pending.Remove(request);
            _pending.Add(request, new PendingSegment { Segment = segment, ExternalSegmentData = externalSegmentData });
        }
    }

    public static bool TryTake(System.Net.HttpWebRequest request, out PendingSegment pendingSegment)
    {
        lock (_lock)
        {
            if (_pending.TryGetValue(request, out pendingSegment))
            {
                _pending.Remove(request);
                return true;
            }

            pendingSegment = null;
            return false;
        }
    }
}
