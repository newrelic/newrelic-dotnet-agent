// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transactions
{
    [NeedSerializableContainer]
    public interface IInternalTransaction : ITransaction, ITransactionExperimental
    {
        /// <summary>
        /// Returns a list of the segments in the transaction.  The segment list is always
        /// ordered by the segment creation time.  A segment will always be preceeded in the list by its 
        /// parent segment (unless it is a root segment).
        /// </summary>
        IList<Segment> Segments { get; }
        ICandidateTransactionName CandidateTransactionName { get; }
        void RollupTransactionNameByStatusCodeIfNeeded();
        ITransactionMetadata TransactionMetadata { get; }
        ICallStackManager CallStackManager { get; }
        int UnitOfWorkCount { get; }
        int NestedTransactionAttempts { get; }
        ImmutableTransaction ConvertToImmutableTransaction();
        int NoticeUnitOfWorkBegins();
        int NoticeUnitOfWorkEnds();
        int NoticeNestedTransactionAttempt();
        void IgnoreAutoBrowserMonitoringForThisTx();
        void IgnoreAllBrowserMonitoringForThisTx();
        void IgnoreApdex();

        ITracingState TracingState { get; }
        string TraceId { get; }
        float Priority { get; }
        bool? Sampled { get; }
        void SetSampled(IAdaptiveSampler adaptiveSampler);

        /// <summary>
        /// Marks this builder as cleanly finished.
        /// </summary>
        /// <returns>true if the transaction actually finished, and false if it was already finished.</returns>
        bool Finish();

        /// <summary>
        /// Forces the duration of the builder to a particular value, regardless of how long it has actually run for
        /// </summary>
        void ForceChangeDuration(TimeSpan duration);

        // There are some cases where we need access to information prior to the completion 
        // of the transaction. Here are the gettter methods for accessing that data.
        // Note that this could be access on different threads.
        bool Ignored { get; }

        bool IgnoreAutoBrowserMonitoring { get; }

        bool IgnoreAllBrowserMonitoring { get; }
        // This guid is created during the transaction initizialiaztion
        string Guid { get; }


        // Used for RUM and CAT to get the duration until this point in time
        TimeSpan GetDurationUntilNow();

        TimeSpan? ResponseTime { get; }

        /// <summary>
        /// Attempts to capture the response time for the transaction.
        /// </summary>
        /// <returns>true if the response time was captured, and false if the response time was previously captured.</returns>
        bool TryCaptureResponseTime();

        ITransactionSegmentState GetTransactionSegmentState();

        void NoticeError(ErrorData errorData);

        /// <summary>
        /// Harvests the log events from the transaction. After doing this, no more logs can be added to the transaction.
        /// </summary>
        /// <returns>The accumulated logs on the first call, or null if logs have already been harvested</returns>
        IList<LogEventWireModel> HarvestLogEvents();

        /// <summary>
        /// Attempts to add a log to the current transaction. Logs cannot be added after the transaction transform has
        /// harvested logs.
        /// </summary>
        /// <param name="logEvent">The log event to add</param>
        /// <returns>true if the log was added, false if the log was unable to be added</returns>
        bool AddLogEvent(LogEventWireModel logEvent);
    }
}
