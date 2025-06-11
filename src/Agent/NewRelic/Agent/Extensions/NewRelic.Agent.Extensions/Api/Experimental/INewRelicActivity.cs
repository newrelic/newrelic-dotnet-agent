// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;

namespace NewRelic.Agent.Extensions.Api.Experimental
{
    public interface INewRelicActivity : IDisposable
    {
        /// <summary>
        /// Gets the underlying activity as a dynamic type.
        /// Provides a way to access properties that are not directly exposed in this interface
        /// </summary>
        dynamic DynamicActivity { get; }

        string SpanId { get; }
        string TraceId { get; }
        string DisplayName { get; }
        bool IsStopped { get; }
        int Kind { get; }

        // can't use a Segment {get; set;} property here because it causes a circular reference between Activity and Segment
        void SetSegment(ISegment segment);
        ISegment GetSegment();

        void Start();

        void Stop();
    }
}
