using System;
using System.Linq;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi
{
    public interface ITransactionFinalizer
    {
        /// <summary>
        /// Performs all the work necessary to cleanly finish an internal transaction. 
        /// </summary>
        /// <param name="transaction"></param>
        void Finish(ITransaction transaction);
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

        public void Finish(ITransaction transaction)
        {
            transaction.Finish();
            UpdatePathHash(transaction);
        }

        private void OnTransactionFinalized(TransactionFinalizedEvent eventData)
        {
            var internalTransaction = eventData.Transaction;

            // When a transaction gets finalized it means it never ended cleanly, so we should try to estimate when it ended based on its last finished segment
            var finishedTransaction = internalTransaction.ConvertToImmutableTransaction();
            var lastStartedSegment = TryGetLastStartedSegment(finishedTransaction);
            var lastFinishedSegment = TryGetLastFinishedSegment(finishedTransaction);
            var estimatedDuration = GetEstimatedTransactionDuration(internalTransaction, lastStartedSegment, lastFinishedSegment);

            try
            {
                internalTransaction.ForceChangeDuration(estimatedDuration);

                // Then we should mark the transaction as cleanly finished so it won't get finalized again
                Finish(internalTransaction);

                // Then we send it off to be transformed as with normal transactions
                _transactionTransformer.Transform(internalTransaction);
            }
            finally
            {
                // Finally, we announce the event to our agent health reporter
                var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(finishedTransaction.TransactionName);
                var lastFinishedSegmentName = lastFinishedSegment != null
                    ? lastFinishedSegment.GetTransactionTraceName()
                    : "<unknown>";
                var lastStartedSegmentName = lastStartedSegment != null
                    ? lastStartedSegment.GetTransactionTraceName()
                    : "<unknown>";
                _agentHealthReporter.ReportTransactionGarbageCollected(transactionMetricName, lastStartedSegmentName, lastFinishedSegmentName);
            }
        }
        private static Segment TryGetLastStartedSegment(ImmutableTransaction transaction)
        {
            // sacksman - this seems like bullshit.  The last segment should always be the last one in our list, right?
            return transaction.Segments
                .Where(segment => segment != null)
                .OrderByDescending(segment => segment.RelativeStartTime)
                .FirstOrDefault();
        }
        private static Segment TryGetLastFinishedSegment(ImmutableTransaction transaction)
        {
            return transaction.Segments
                .Where(segment => segment != null)
                .Where(segment => segment.Duration != null)
                .OrderByDescending(segment => segment.RelativeStartTime + segment.Duration.Value)
                .FirstOrDefault();
        }

        /// <summary>
        /// Estimates the duration of a transaction based on its segments.
        /// </summary>
        /// <returns>An estimate of the duration of a transaction.</returns>
        private static TimeSpan GetEstimatedTransactionDuration(ITransaction internalTransaction, Segment lastStartedSegment, Segment lastFinishedSegment)
        {
            if (lastStartedSegment == null && lastFinishedSegment == null)
                return TimeSpan.FromMilliseconds(1);

            var immutableTransaction = internalTransaction.ConvertToImmutableTransaction();

            var lastStartedSegmentEndTime = lastStartedSegment?.CalculatedRelativeEndTime ?? new TimeSpan();
            var lastFinishedSegmentEndTime = lastFinishedSegment?.CalculatedRelativeEndTime ?? new TimeSpan();
            var maxEndTime = DateTimeMath.Max(lastStartedSegmentEndTime, lastFinishedSegmentEndTime);

            return maxEndTime;
        }

        private void UpdatePathHash(ITransaction transaction)
        {
            var currentTransactionName = transaction.CandidateTransactionName.CurrentTransactionName;
            var currentTransactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(currentTransactionName);
            var referrerPathHash = transaction.TransactionMetadata.CrossApplicationReferrerPathHash;

            var newPathHash = _pathHashMaker.CalculatePathHash(currentTransactionMetricName.PrefixedName, referrerPathHash);

            transaction.TransactionMetadata.SetCrossApplicationPathHash(newPathHash);
        }
    }
}
