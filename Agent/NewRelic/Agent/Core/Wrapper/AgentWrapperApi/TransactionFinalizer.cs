using System;
using System.Linq;
using JetBrains.Annotations;
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
		/// <returns>true if the transaction got finalized, and false if it was already finalized.</returns>
		bool Finish([NotNull] ITransaction transaction);
	}

	public class TransactionFinalizer : DisposableService, ITransactionFinalizer
	{
		[NotNull]
		private readonly IAgentHealthReporter _agentHealthReporter;

		[NotNull]
		private readonly ITransactionMetricNameMaker _transactionMetricNameMaker;

		[NotNull]
		private readonly IPathHashMaker _pathHashMaker;

		[NotNull]
		private readonly ITransactionTransformer _transactionTransformer;

		public TransactionFinalizer([NotNull] IAgentHealthReporter agentHealthReporter, [NotNull] ITransactionMetricNameMaker transactionMetricNameMaker, [NotNull] IPathHashMaker pathHashMaker, [NotNull] ITransactionTransformer transactionTransformer)
		{
			_agentHealthReporter = agentHealthReporter;
			_transactionMetricNameMaker = transactionMetricNameMaker;
			_pathHashMaker = pathHashMaker;
			_transactionTransformer = transactionTransformer;

			_subscriptions.Add<TransactionFinalizedEvent>(OnTransactionFinalized);
		}

		public bool Finish(ITransaction transaction)
		{

			if (transaction.Finish())
			{
				UpdatePathHash(transaction);
				return true;
			}

			return false;
		}

		private void OnTransactionFinalized([NotNull] TransactionFinalizedEvent eventData)
		{
			var internalTransaction = eventData.Transaction;

			// When a transaction gets finalized it means it never ended cleanly, so we should try to estimate when it ended based on its last finished segment
			var immutableTransaction = internalTransaction.ConvertToImmutableTransaction();
			var lastStartedSegment = TryGetLastStartedSegment(immutableTransaction);
			var lastFinishedSegment = TryGetLastFinishedSegment(immutableTransaction);
			var estimatedDuration = GetEstimatedTransactionDuration(internalTransaction, lastStartedSegment, lastFinishedSegment);
			bool finishedTransaction = false;

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
					_agentHealthReporter.ReportTransactionGarbageCollected(transactionMetricName, lastStartedSegmentName, lastFinishedSegmentName);
				}
			}
		}

		[CanBeNull]
		private static Segment TryGetLastStartedSegment([NotNull] ImmutableTransaction transaction)
		{
			return transaction.Segments.LastOrDefault(); 
		}

		[CanBeNull]
		private static Segment TryGetLastFinishedSegment([NotNull] ImmutableTransaction transaction)
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
		private static TimeSpan GetEstimatedTransactionDuration([NotNull] ITransaction internalTransaction, [CanBeNull] Segment lastStartedSegment, [CanBeNull] Segment lastFinishedSegment)
		{
			if (lastStartedSegment == null && lastFinishedSegment == null)
				return TimeSpan.FromMilliseconds(1);

			var immutableTransaction = internalTransaction.ConvertToImmutableTransaction();

			var lastStartedSegmentEndTime = lastStartedSegment?.CalculatedRelativeEndTime ?? new TimeSpan();
			var lastFinishedSegmentEndTime = lastFinishedSegment?.CalculatedRelativeEndTime ?? new TimeSpan();
			var maxEndTime = DateTimeMath.Max(lastStartedSegmentEndTime, lastFinishedSegmentEndTime);

			return maxEndTime;
		}

		private void UpdatePathHash([NotNull] ITransaction transaction)
		{
			var currentTransactionName = transaction.CandidateTransactionName.CurrentTransactionName;
			var currentTransactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(currentTransactionName);
			var referrerPathHash = transaction.TransactionMetadata.CrossApplicationReferrerPathHash;

			var newPathHash = _pathHashMaker.CalculatePathHash(currentTransactionMetricName.PrefixedName, referrerPathHash);

			transaction.TransactionMetadata.SetCrossApplicationPathHash(newPathHash);
		}
	}
}
