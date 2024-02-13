// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Api
{
    public interface ISegment : ISpan
    {
        /// <summary>
        /// Returns true if this is a real (not a no op) segment.
        /// </summary>
        /// <returns></returns>
        bool IsValid { get; }

        /// <summary>
        /// Returns true if the duration of this segment should not count towards the parent's exclusive time when async.
        /// </summary>
        bool DurationShouldBeDeductedFromParent { get; set; }

        /// <summary>
        /// If sets to true, the duration of its children won't be added to it's exclusive time when async.
        /// </summary>
        bool AlwaysDeductChildDuration { set; }

        /// <summary>
        /// Returns true if this is a leaf segment.
        /// </summary>
        bool IsLeaf { get; }

        /// <summary>
        /// Returns true if it is an external segment.
        /// </summary>
        bool IsExternal { get; }

        /// <summary>
        /// Returns the current segment's Span Id.
        /// </summary>
        string SpanId { get; }

        /// <summary>
        /// Provides the ability to override a segment name. If this is anything other than null or empty,
        /// then this value should be used as the segment/span name.
        /// </summary>
        string SegmentNameOverride { get; set; }

        /// <summary>
        /// Ends this transaction segment.
        /// </summary>
        void End();

        /// <summary>
        /// Ends this StackExchange.Redis transaction segment.
        /// </summary>
        void EndStackExchangeRedis();

        /// <summary>
        /// Ends this transaction segment in the exception case.
        /// </summary>
        void End(Exception ex);

        /// <summary>
        /// Marks this segment as combinable, which means that identical adjacent siblings that are also combinable will be combined into one segment. This is useful for segments that are tracking methods like `SqlDataReader.Read` which often gets called many times back to back, and where it is typically more interesting to see the segments aggregated together rather than separate.
        /// </summary>
        void MakeCombinable();

        /// <summary>
        /// Removes this segment from the top of the agent's internal call stack. Should only be used for asynchronous methods. Calling EndSegment is sufficient for synchronous methods.
        /// </summary>
        void RemoveSegmentFromCallStack();

        /// <summary>
        /// Sets the Destination on SegmentData, if the data is of type MessageBrokerSegmentData
        /// </summary>
        /// <param name="destination"></param>
        void SetMessageBrokerDestination(string destination);

        /// <summary>
        /// Gets the duration of the segment if the segment has finished or a TimeSpan.Zero if not.
        /// </summary>
        TimeSpan DurationOrZero { get; }
    }
}
