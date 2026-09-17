// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.ContinuousProfiling;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Transactions;

public interface ITransactionFinalizer
{
    /// <summary>
    /// Performs all the work necessary to cleanly finish an internal transaction. 
    /// </summary>
    /// <param name="transaction"></param>
    /// <returns>true if the transaction got finalized, and false if it was already finalized.</returns>
    bool Finish(IInternalTransaction transaction);
}

public class TransactionFinalizer : DisposableService, ITransactionFinalizer
{
    private readonly IAgentHealthReporter _agentHealthReporter;
    private readonly ITransactionMetricNameMaker _transactionMetricNameMaker;
    private readonly IPathHashMaker _pathHashMaker;
    private readonly ITransactionTransformer _transactionTransformer;

    public TransactionFinalizer(IAgentHealthReporter agentHealthReporter, ITransactionMetricNameMaker transactionMetricNameMaker, IPathHashMaker pathHashMaker, ITransactionTransformer transactionTransformer)
    {
        _agentHealthReporter = agentHealthReporter;
        _transactionMetricNameMaker = transactionMetricNameMaker;
        _pathHashMaker = pathHashMaker;
        _transactionTransformer = transactionTransformer;
        _subscriptions.Add<TransactionFinalizedEvent>(OnTransactionFinalized);
    }

    public bool Finish(IInternalTransaction transaction)
    {
        if (transaction.Finish())
        {
            UpdatePathHash(transaction);
            RetireContinuousProfilingSpans(transaction);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Marks every span this transaction pushed to the native continuous profiler as ended, so no later
    /// CPU sample is linked to it.
    ///
    /// <para>Lives here, not in <c>Transaction.End()</c>, because this is the one choke point EVERY
    /// terminating transaction passes through exactly once: a transaction that never ends cleanly is
    /// reaped via <c>~Transaction()</c> -&gt; <c>TransactionFinalizedEvent</c> -&gt; <c>OnTransactionFinalized</c>,
    /// which never runs <c>End()</c>'s body. <c>Transaction.Finish()</c> is a double-checked-lock gate that
    /// returns true to exactly one caller ever, so this cannot run twice for one transaction.</para>
    ///
    /// <para>Note this can run on the GC FINALIZER THREAD (the reaped path). It must therefore never
    /// block, never take a lock an application thread could hold while the runtime is suspended, and never
    /// throw -- an exception escaping here is swallowed by the destructor and would silently lose the
    /// transaction, and a stall would stall every finalizer in the process. The <c>Segments</c> walk below
    /// does take one lock -- <c>ConcurrentList&lt;T&gt;</c>'s <c>ReaderWriterLockSlim</c> READ lock -- and
    /// that is deliberately accepted, not an oversight: the identical lock is already taken on this same
    /// finalizer path by <c>ConvertToImmutableTransaction</c> (<c>ImmutableTransaction</c>'s ctor copies
    /// <c>Segments</c>), and a finalizer runs with the world RESUMED, so no writer can be suspended while
    /// holding it. The rule being stated is about the CP-suspend-window reader, which never runs here.</para>
    ///
    /// <para>Deliberately probes for ALREADY-MATERIALIZED span ids rather than reading
    /// <c>Segment.SpanId</c>: that getter is a lazy generator, so reading it would mint ids for spans that
    /// are never reported (an unsampled transaction generates none today). An id that was never
    /// materialized was never pushed, so there is nothing to retire.</para>
    ///
    /// <para><c>Transaction.End()</c>'s existing <c>ResetTraceContext()</c> call stays exactly where it is
    /// and is NOT moved here: it is thread-affine (it clears the calling thread's own native slot and a
    /// <c>[ThreadStatic]</c> guard), so running it on the finalizer thread would both miss the thread that
    /// actually pushed and wrongly tombstone the finalizer thread's slot.</para>
    /// </summary>
    private static void RetireContinuousProfilingSpans(IInternalTransaction transaction)
    {
        // Single static volatile read for every customer without continuous profiling: no enumeration of
        // Segments, no allocation, no interface dispatch.
        if (!ContinuousProfilingContext.AnyEnabled)
            return;

        try
        {
            // AnyEnabled is armed ahead of the context going live and is explicitly "never the final
            // word", so ask the context itself before doing any work: a retirement issued while the
            // native side is not live is dropped there anyway, and the next Start() bumps the trace
            // context generation, which invalidates every slot written before it.
            var context = ContinuousProfilingContext.Instance;
            if (!context.IsEnabled)
                return;

            List<string> spanIds = null;

            // ConcurrentList<Segment>.GetEnumerator takes the read lock once and enumerates a defensive
            // copy, so a straggler async continuation adding a segment cannot throw here. Entries can be
            // null: a transaction past TransactionTracerMaxSegments has its over-limit slots nulled out.
            //
            // KNOWN RESIDUAL, and it is that same defensive copy: this walk is a POINT-IN-TIME SNAPSHOT.
            // A straggler async continuation that starts a segment and pushes its span id AFTER this
            // enumeration is not retired by it, so the thread that pushed it keeps a link to an
            // already-finished transaction. That link survives until the pushing thread's next
            // instrumented push overwrites its slot (the common self-heal) or the next CP Start() bumps
            // the trace-context generation. This is a stated bound of retiring at a single choke point,
            // not a bug to fix here: the alternative -- capturing every span id at push time -- puts an
            // append and an allocation on the per-instrumented-call hot path, which the design forbids.
            // Unreachable on the reaped path, where unreachability precludes stragglers by construction.
            foreach (var segment in transaction.Segments)
            {
                var spanId = segment?.TryGetMaterializedSpanId();
                if (spanId == null)
                    continue;

                spanIds = spanIds ?? new List<string>();
                spanIds.Add(spanId);
            }

            // Segments dropped for exceeding the max-segments cap are unreachable from Segments above but
            // had already pushed their ids, so they must be retired too.
            foreach (var droppedSpanId in transaction.DroppedSegmentSpanIds)
            {
                if (droppedSpanId == null)
                    continue;

                spanIds = spanIds ?? new List<string>();
                spanIds.Add(droppedSpanId);
            }

            if (spanIds == null)
                return; // nothing was ever materialized -> nothing was ever pushed.

            // Hex strings, one call for the whole transaction: the context is the single decoder, and it
            // already drops unusable ids and skips the native call when nothing survives.
            context.RetireSpans(spanIds);
        }
        catch (Exception ex)
        {
            // Never let continuous profiling break transaction finalization. See the finalizer-thread note
            // above for why this catch is not optional.
            Log.Finest(ex, "[ContinuousProfiling] Failed to retire ended spans for a finished transaction.");
        }
    }

    private void OnTransactionFinalized(TransactionFinalizedEvent eventData)
    {
        var internalTransaction = eventData.Transaction;

        // When a transaction gets finalized it means it never ended cleanly, so we should try to estimate when it ended based on its last finished segment
        var immutableTransaction = internalTransaction.ConvertToImmutableTransaction();
        var lastStartedSegment = TryGetLastStartedSegment(immutableTransaction);
        var lastFinishedSegment = TryGetLastFinishedSegment(immutableTransaction);
        var estimatedDuration = GetEstimatedTransactionDuration(internalTransaction, lastStartedSegment, lastFinishedSegment);
        var finishedTransaction = false;

        try
        {
            internalTransaction.ForceChangeDuration(estimatedDuration);

            // Then we should mark the transaction as cleanly finished so it won't get finalized again
            finishedTransaction = Finish(internalTransaction);

            if (finishedTransaction)
            {
                // Then we send it off to be transformed as with normal transactions
                _transactionTransformer.Transform(internalTransaction);
            }
        }
        finally
        {
            if (finishedTransaction)
            {
                // Finally, we announce the event to our agent health reporter
                var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
                var lastFinishedSegmentName = lastFinishedSegment != null
                    ? lastFinishedSegment.GetTransactionTraceName()
                    : "<unknown>";
                var lastStartedSegmentName = lastStartedSegment != null
                    ? lastStartedSegment.GetTransactionTraceName()
                    : "<unknown>";
                _agentHealthReporter.ReportTransactionGarbageCollected(internalTransaction.Guid, transactionMetricName, lastStartedSegmentName, lastFinishedSegmentName);
            }
        }
    }

    private static Segment TryGetLastStartedSegment(ImmutableTransaction transaction)
    {
        return transaction.Segments.LastOrDefault();
    }

    private static Segment TryGetLastFinishedSegment(ImmutableTransaction transaction)
    {
        return transaction.Segments
            .Where(segment => segment.RelativeEndTime != null)
            .OrderByDescending(segment => segment.RelativeEndTime)
            .FirstOrDefault();
    }

    /// <summary>
    /// Estimates the duration of a transaction based on its segments.
    /// </summary>
    /// <returns>An estimate of the duration of a transaction.</returns>
    private static TimeSpan GetEstimatedTransactionDuration(IInternalTransaction internalTransaction, Segment lastStartedSegment, Segment lastFinishedSegment)
    {
        if (lastStartedSegment == null && lastFinishedSegment == null)
            return TimeSpan.FromMilliseconds(1);

        var immutableTransaction = internalTransaction.ConvertToImmutableTransaction();

        var lastStartedSegmentEndTime = lastStartedSegment?.CalculatedRelativeEndTime ?? new TimeSpan();
        var lastFinishedSegmentEndTime = lastFinishedSegment?.CalculatedRelativeEndTime ?? new TimeSpan();
        var maxEndTime = DateTimeMath.Max(lastStartedSegmentEndTime, lastFinishedSegmentEndTime);

        return maxEndTime;
    }

    private void UpdatePathHash(IInternalTransaction transaction)
    {
        var currentTransactionName = transaction.CandidateTransactionName.CurrentTransactionName;
        var currentTransactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(currentTransactionName);
        var referrerPathHash = transaction.TransactionMetadata.CrossApplicationReferrerPathHash;

        var newPathHash = _pathHashMaker.CalculatePathHash(currentTransactionMetricName.PrefixedName, referrerPathHash);

        transaction.TransactionMetadata.SetCrossApplicationPathHash(newPathHash);
    }
}