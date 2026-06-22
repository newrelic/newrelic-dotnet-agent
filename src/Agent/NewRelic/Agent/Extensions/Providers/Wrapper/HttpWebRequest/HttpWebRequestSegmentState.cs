// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;

namespace NewRelic.Providers.Wrapper.HttpWebRequest;

// Shares the external segment created on the request-stream path (POST/PUT) with the response
// path, so a single external segment spans the whole request/response and the distributed-trace
// headers are injected by SerializeHeaders while that segment is the current segment - which
// makes the downstream parent the external client span rather than the calling segment.
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
