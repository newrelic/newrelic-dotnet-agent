// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;

namespace NewRelic.Agent.Extensions.Api.Experimental
{
    public interface INewRelicActivity : IDisposable
    {
        string SpanId { get; }
        string TraceId { get; }
        string DisplayName { get; }
        bool IsStopped { get; }

        // can't use a Segment {get; set;} property here because it causes a circular reference between Activity and Segment
        void SetSegment(ISegment segment);
        ISegment GetSegment();

        void Start();

        void Stop();
    }
}
