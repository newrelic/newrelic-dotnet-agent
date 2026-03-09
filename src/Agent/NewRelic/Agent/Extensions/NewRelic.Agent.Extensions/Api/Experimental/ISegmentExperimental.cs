// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Api.Experimental;

namespace NewRelic.Agent.Api.Experimental;

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

    /// <summary>
    /// Returns the activity associated with the segment.
    /// </summary>
    /// <returns></returns>
    INewRelicActivity GetActivity();

    /// <summary>
    /// Add object to the segment's data cache with the specified key.
    /// This is intended for use in cases where you need to store some data during the execution of a segment that will be used when creating the segment data or attributes.
    /// This should not be used as a general purpose storage mechanism and should not be used to store large objects or large amounts of data.
    /// This will overwrite any existing value in the cache with the same key, so it should be used with unique keys to avoid collisions.
    /// It is the caller's responsibility to manage the keys and ensure they are unique within the context of a segment.
    ///
    /// Notes:
    /// Null keys are not allowed and will be ignored.  If a null key is passed to this method, the method will return without adding anything to the cache.
    /// Null values are allowed and will be stored in the cache.  If a null value is added to the cache, it will overwrite any existing value with the same key, and a subsequent retrieval of that key from the cache will return null.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    void AddCacheItem(string key, object value);
}
