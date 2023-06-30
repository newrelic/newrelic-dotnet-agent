// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Api.Experimental
{
    /// <summary>
    /// This interface contains methods we may eventually move to <see cref="ISegment"/> once they have been sufficiently vetted.
    /// Methods on this interface are subject to refactoring or removal in future versions of the API.
    /// </summary>
    public interface ISegmentExperimental
    {
        /// <summary>
        /// Gets the ISegmentData currently associated to the segment. This is useful when the logic for managing
        /// the segment data is split across multiple instrumentation classes.
        /// </summary>
        ISegmentData SegmentData { get; }

        /// <summary>
        /// Adds the provided segmentData to the segment. This data replaces any previously set segmentData
        /// on the segment. This data should be added before the segment ends.
        /// </summary>
        /// <param name="segmentData">The data to add to the segment.</param>
        /// <returns>The segment that the segmentData was added to.</returns>
        ISegmentExperimental SetSegmentData(ISegmentData segmentData);

        /// <summary>
        /// Makes the segment a leaf segment. Leaf segments will prevent other
        /// instrumented methods from running while the leaf segment is currently on the call stack.
        /// </summary>
        /// <returns>The segment that the segmentData was added to.</returns>
        ISegmentExperimental MakeLeaf();

        /// <summary>
        /// Get or set the function (method) name for the user/customer code represented
        /// by the instrumentation. This only needs set when the instrumentation point and
        /// the customer code represented differ. For example, controller actions.
        /// </summary>
        string UserCodeFunction { get; set; }

        /// <summary>
        /// Get or set the namespace (type) name for the user/customer code represented
        /// by the instrumentation. This only needs set when the instrumentation point and
        /// the customer code represented differ. For example, controller actions.
        /// </summary>
        string UserCodeNamespace { get; set; }

        /// <summary>
        /// Returns the category of the segment.
        /// </summary>
        /// <returns>Category of the segment.</returns>
        string GetCategory();

        /// <summary>
        /// Will be true if a relative end time has been set on the segment.  In most situations, this is only set when a segment is ended.
        /// </summary>
        bool IsDone { get; }
    }
}
