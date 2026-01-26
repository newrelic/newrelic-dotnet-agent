// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Segments;

namespace NewRelic.Agent.Core.Transactions;

public interface ITransactionSegmentState
{
    /// <summary>
    /// Returns a time span relative to the start of the transaction (the 
    /// transaction's duration up to now).
    /// </summary>
    /// <returns></returns>
    TimeSpan GetRelativeTime();
    DateTime StartTime { get; }

    /// <summary>
    /// Returns the segment id on the top of the current call stack;
    /// </summary>
    /// <returns></returns>
    int? ParentSegmentId();

    /// <summary>
    /// Pushes a segment onto the call stack and adds it to the list of segments.
    /// Returns the unique id of the segment.
    /// </summary>
    /// <param name="segment"></param>
    int CallStackPush(Segment segment);

    /// <summary>
    /// Pops a segment off of the call stack.
    /// </summary>
    /// <param name="segment"></param>
    /// <param name="notifyParent">Notify parent will be true when a segment has ended.  In the async case this is when the task completes.</param>
    void CallStackPop(Segment segment, bool notifyParent = false);

    int CurrentManagedThreadId { get; }

    IAttributeDefinitions AttribDefs { get; }

    IErrorService ErrorService { get; }
}
