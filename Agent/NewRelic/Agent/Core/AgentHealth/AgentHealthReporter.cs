using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Collections;
using System;
using System.Collections.Generic;
using System.Net;

namespace NewRelic.Agent.Core.AgentHealth
{
	public interface IAgentHealthReporter
	{
		void ReportAgentVersion([NotNull] string agentVersion, [NotNull] string hostName);
		void ReportTransactionEventReservoirResized(uint newSize);
		void ReportTransactionEventCollected();
		void ReportTransactionEventsRecollected(int count);
		void ReportTransactionEventsSent(int count);
		void ReportCustomEventReservoirResized(uint newSize);
		void ReportCustomEventCollected();
		void ReportCustomEventsRecollected(int count);
		void ReportCustomEventsSent(int count);
		void ReportErrorTraceCollected();
		void ReportErrorTracesRecollected(int count);
		void ReportErrorTracesSent(int count);
		void ReportErrorEventSeen();
		void ReportErrorEventsSent(int count);
		void ReportSqlTracesRecollected(int count);
		void ReportSqlTracesSent(int count);

		void ReportTransactionGarbageCollected(TransactionMetricName transactionMetricName, [NotNull] string lastStartedSegmentName, [NotNull] string lastFinishedSegmentName);

		void ReportWrapperShutdown([NotNull] IWrapper wrapper, [NotNull] Method method);
		void ReportAgentApiMethodCalled([NotNull] string methodName);
		void ReportIfHostIsLinuxOs();
		void ReportBootIdError();
		void ReportAwsUtilizationError();
		void ReportAzureUtilizationError();
		void ReportPcfUtilizationError();
		void ReportGcpUtilizationError();

		/// <summary>Created when AcceptDistributedTracePayload was called successfully</summary>
		void ReportSupportabilityDistributedTraceAcceptPayloadSuccess();

		/// <summary>Created when AcceptDistributedTracePayload had a generic exception</summary>
		void ReportSupportabilityDistributedTraceAcceptPayloadException();

		/// <summary>Created when AcceptDistributedTracePayload had a parsing exception</summary>
		void ReportSupportabilityDistributedTraceAcceptPayloadParseException();

		/// <summary>Created when AcceptDistributedTracePayload was ignored because CreatePayload had already been called</summary>
		void ReportSupportabilityDistributedTraceAcceptPayloadIgnoredCreateBeforeAccept();

		/// <summary>Created when AcceptDistributedTracePayload was ignored because AcceptPayload had already been called</summary>
		void ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMultiple();

		/// <summary>Created when AcceptDistributedTracePayload was ignored because the payload's major version was greater than the agent's</summary>
		void ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMajorVersion();

		/// <summary>Created when AcceptDistributedTracePayload was ignored because the payload was null</summary>
		void ReportSupportabilityDistributedTraceAcceptPayloadIgnoredNull();

		/// <summary>Created when AcceptDistributedTracePayload was ignored because the payload was untrusted</summary>
		void ReportSupportabilityDistributedTraceAcceptPayloadIgnoredUntrustedAccount();

		/// <summary>Created when CreateDistributedTracePayload was called successfully</summary>
		void ReportSupportabilityDistributedTraceCreatePayloadSuccess();

		/// <summary>Created when CreateDistributedTracePayload had a generic exception</summary>
		void ReportSupportabilityDistributedTraceCreatePayloadException();

		void ReportSupportabilityCollectorErrorException(string endpointMethod, TimeSpan responseDuration, HttpStatusCode? statusCode);
	}

	public class AgentHealthReporter : DisposableService, IAgentHealthReporter, IOutOfBandMetricSource
	{
		private static readonly TimeSpan TimeBetweenExecutions = TimeSpan.FromMinutes(1);

		[NotNull]
		private readonly IMetricBuilder _metricBuilder;

		[NotNull]
		private readonly IScheduler _scheduler;

		[CanBeNull]
		private PublishMetricDelegate _publishMetricDelegate;

		[NotNull]
		private readonly IList<RecurringLogData> _recurringLogDatas = new ConcurrentList<RecurringLogData>();

		[NotNull]
		private readonly IDictionary<AgentHealthEvent, InterlockedCounter> _agentHealthEventCounters = new Dictionary<AgentHealthEvent, InterlockedCounter>();

		public AgentHealthReporter([NotNull] IMetricBuilder metricBuilder, [NotNull] IScheduler scheduler)
		{
			_metricBuilder = metricBuilder;
			_scheduler = scheduler;
			_scheduler.ExecuteEvery(LogRecurringLogs, TimeBetweenExecutions);

			var agentHealthEvents = Enum.GetValues(typeof(AgentHealthEvent)) as AgentHealthEvent[];
			foreach(var agentHealthEvent in agentHealthEvents)
			{
				_agentHealthEventCounters[agentHealthEvent] = new InterlockedCounter();
			}
		}

		public override void Dispose()
		{
			base.Dispose();
			_scheduler.StopExecuting(LogRecurringLogs);
		}

		private void LogRecurringLogs()
		{
			foreach(var data in _recurringLogDatas)
			{
				data?.LogAction(data.Message);
			}

			foreach(var counter in _agentHealthEventCounters)
			{
				if ( counter.Value != null && counter.Value.Value > 0)
				{
					var agentHealthEvent = counter.Key;
					var timesOccured = counter.Value.Exchange(0);
					Log.Info($"Event {agentHealthEvent} has occurred {timesOccured} times in the last {TimeBetweenExecutions.TotalSeconds} seconds");
				}
			}
		}

		public void ReportAgentVersion(string agentVersion, string hostName)
		{
			TrySend(_metricBuilder.TryBuildAgentVersionMetric(agentVersion));
			TrySend(_metricBuilder.TryBuildAgentVersionByHostMetric(hostName, agentVersion));
		}

		#region TransactionEvents

		public void ReportTransactionEventReservoirResized(uint newSize)
		{
			TrySend(_metricBuilder.TryBuildTransactionEventReservoirResizedMetric());

			Log.Warn("Resizing transaction event reservoir to " + newSize + " events.");
		}

		public void ReportTransactionEventCollected()
		{
			TrySend(_metricBuilder.TryBuildTransactionEventsCollectedMetric());

			// Note: this metric is REQUIRED by APM (see https://source.datanerd.us/agents/agent-specs/pull/84)
			TrySend(_metricBuilder.TryBuildTransactionEventsSeenMetric());
		}

		public void ReportTransactionEventsRecollected(int count) => TrySend(_metricBuilder.TryBuildTransactionEventsRecollectedMetric(count));

		public void ReportTransactionEventsSent(int count) => TrySend(_metricBuilder.TryBuildTransactionEventsSentMetric(count));

		#endregion TransactionEvents

		#region CustomEvents

		public void ReportCustomEventReservoirResized(uint newSize)
		{
			TrySend(_metricBuilder.TryBuildCustomEventReservoirResizedMetric());

			Log.Warn("Resizing custom event reservoir to " + newSize + " events.");
		}

		public void ReportCustomEventCollected()
		{
			TrySend(_metricBuilder.TryBuildCustomEventsCollectedMetric());
			// Note: Though not required by APM like the transaction event supportability metrics, this metric should still be created to maintain consistency
			TrySend(_metricBuilder.TryBuildCustomEventsSeenMetric());
		}

		public void ReportCustomEventsRecollected(int count) => TrySend(_metricBuilder.TryBuildCustomEventsRecollectedMetric(count));

		// Note: Though not required by APM like the transaction event supportability metrics, this metric should still be created to maintain consistency
		public void ReportCustomEventsSent(int count) => TrySend(_metricBuilder.TryBuildCustomEventsSentMetric(count));

		#endregion CustomEvents

		#region ErrorTraces

		public void ReportErrorTraceCollected() => TrySend(_metricBuilder.TryBuildErrorTracesCollectedMetric());

		public void ReportErrorTracesRecollected(int count) => TrySend(_metricBuilder.TryBuildErrorTracesRecollectedMetric(count));

		public void ReportErrorTracesSent(int count) => TrySend(_metricBuilder.TryBuildErrorTracesSentMetric(count));

		#endregion ErrorTraces

		#region ErrorEvents

		public void ReportErrorEventSeen() => TrySend(_metricBuilder.TryBuildErrorEventsSeenMetric());

		public void ReportErrorEventsSent(int count) => TrySend(_metricBuilder.TryBuildErrorEventsSentMetric(count));

		#endregion ErrorEvents

		#region SqlTraces

		public void ReportSqlTracesRecollected(int count) => TrySend(_metricBuilder.TryBuildSqlTracesRecollectedMetric(count));

		public void ReportSqlTracesSent(int count) => TrySend(_metricBuilder.TryBuildSqlTracesSentMetric(count));

		#endregion ErrorTraces

		public void ReportTransactionGarbageCollected(TransactionMetricName transactionMetricName, string lastStartedSegmentName, string lastFinishedSegmentName)
		{
			var transactionName = transactionMetricName.PrefixedName;
			Log.Debug($"Transaction was garbage collected without ever ending.\nTransaction Name: {transactionName}\nLast Started Segment: {lastStartedSegmentName}\nLast Finished Segment: {lastFinishedSegmentName}");

			_agentHealthEventCounters[AgentHealthEvent.TransactionGarbageCollected]?.Increment();
		}

		public void ReportWrapperShutdown(IWrapper wrapper, Method method)
		{
			var wrapperName = wrapper.GetType().FullName;

			var metrics = new[]
			{
				_metricBuilder.TryBuildAgentHealthEventMetric(AgentHealthEvent.WrapperShutdown, "all"),
				_metricBuilder.TryBuildAgentHealthEventMetric(AgentHealthEvent.WrapperShutdown, $"{wrapperName}/all"),
				_metricBuilder.TryBuildAgentHealthEventMetric(AgentHealthEvent.WrapperShutdown, wrapperName, method.Type.Name, method.MethodName)
			};

			foreach(var metric in metrics)
			{
				TrySend(metric);
			}

			Log.Error($"Wrapper {wrapperName} is being disabled for {method.MethodName} due to too many consecutive exceptions. All other methods using this wrapper will continue to be instrumented. This will reduce the functionality of the agent until the agent is restarted.");

			_recurringLogDatas.Add(new RecurringLogData(Log.Debug, $"Wrapper {wrapperName} was disabled for {method.MethodName} at {DateTime.Now} due to too many consecutive exceptions. All other methods using this wrapper will continue to be instrumented. This will reduce the functionality of the agent until the agent is restarted."));
		}

		public void ReportAgentApiMethodCalled(string methodName) => TrySend(_metricBuilder.TryBuildAgentApiMetric(methodName));

		public void ReportIfHostIsLinuxOs()
		{

#if NETSTANDARD2_0

			bool isLinux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
			var metric =_metricBuilder.TryBuildLinuxOsMetric(isLinux);
			TrySend(metric);
#endif
		}

		public void ReportBootIdError() => TrySend(_metricBuilder.TryBuildBootIdError());

		public void ReportAwsUtilizationError() => TrySend(_metricBuilder.TryBuildAwsUsabilityError());

		public void ReportAzureUtilizationError() => TrySend(_metricBuilder.TryBuildAzureUsabilityError());

		public void ReportPcfUtilizationError() => TrySend(_metricBuilder.TryBuildPcfUsabilityError());

		public void ReportGcpUtilizationError() => TrySend(_metricBuilder.TryBuildGcpUsabilityError());

		#region DistributedTrace

		/// <summary>Created when AcceptDistributedTracePayload was called successfully</summary>
		public void ReportSupportabilityDistributedTraceAcceptPayloadSuccess() =>
			TrySend(_metricBuilder.TryBuildAcceptPayloadSuccess);

		/// <summary>Created when AcceptDistributedTracePayload had a generic exception</summary>
		public void ReportSupportabilityDistributedTraceAcceptPayloadException() =>
			TrySend(_metricBuilder.TryBuildAcceptPayloadException);

		/// <summary>Created when AcceptDistributedTracePayload had a parsing exception</summary>
		public void ReportSupportabilityDistributedTraceAcceptPayloadParseException() =>
			TrySend(_metricBuilder.TryBuildAcceptPayloadParseException);

		/// <summary>Created when AcceptDistributedTracePayload was ignored because CreatePayload had already been called</summary>
		public void ReportSupportabilityDistributedTraceAcceptPayloadIgnoredCreateBeforeAccept() =>
			TrySend(_metricBuilder.TryBuildAcceptPayloadIgnoredCreateBeforeAccept);

		/// <summary>Created when AcceptDistributedTracePayload was ignored because AcceptPayload had already been called</summary>
		public void ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMultiple() =>
			TrySend(_metricBuilder.TryBuildAcceptPayloadIgnoredMultiple);

		/// <summary>Created when AcceptDistributedTracePayload was ignored because the payload's major version was greater than the agent's</summary>
		public void ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMajorVersion() =>
			TrySend(_metricBuilder.TryBuildAcceptPayloadIgnoredMajorVersion);

		/// <summary>Created when AcceptDistributedTracePayload was ignored because the payload was null</summary>
		public void ReportSupportabilityDistributedTraceAcceptPayloadIgnoredNull() =>
			TrySend(_metricBuilder.TryBuildAcceptPayloadIgnoredNull);

		/// <summary>Created when AcceptDistributedTracePayload was ignored because the payload was untrusted</summary>
		public void ReportSupportabilityDistributedTraceAcceptPayloadIgnoredUntrustedAccount() =>
			TrySend(_metricBuilder.TryBuildAcceptPayloadIgnoredUntrustedAccount);

		/// <summary>Created when CreateDistributedTracePayload was called successfully</summary>
		public void ReportSupportabilityDistributedTraceCreatePayloadSuccess() =>
			TrySend(_metricBuilder.TryBuildCreatePayloadSuccess);

		/// <summary>Created when CreateDistributedTracePayload had a generic exception</summary>
		public void ReportSupportabilityDistributedTraceCreatePayloadException() =>
			TrySend(_metricBuilder.TryBuildCreatePayloadException);

		#endregion DistributedTrace

		#region HttpError

		public void ReportSupportabilityCollectorErrorException(string endpointMethod, TimeSpan responseDuration, HttpStatusCode? statusCode)
		{
			if (statusCode.HasValue)
			{
				TrySend(_metricBuilder.TryBuildSupportabilityErrorHttpStatusCodeFromCollector(statusCode.Value));
			}

			TrySend(_metricBuilder.TryBuildSupportabilityEndpointMethodErrorDuration(endpointMethod, responseDuration));
		}

		
		#endregion

		public void RegisterPublishMetricHandler(PublishMetricDelegate publishMetricDelegate)
		{
			if (_publishMetricDelegate != null)
				Log.Warn("Existing PublishMetricDelegate registration being overwritten.");

			_publishMetricDelegate = publishMetricDelegate;
		}

		private void TrySend([CanBeNull] MetricWireModel metric)
		{
			if (metric == null)
				return;

			if (_publishMetricDelegate == null)
			{
				Log.WarnFormat("No PublishMetricDelegate to flush metric '{0}' through.", metric.MetricName.Name);
				return;
			}

			try
			{
				_publishMetricDelegate(metric);
			}
			catch (Exception ex)
			{
				Log.Error(ex);
			}
		}

		private class RecurringLogData
		{
			[NotNull]
			public readonly Action<string> LogAction;

			[NotNull]
			public readonly string Message;

			public RecurringLogData([NotNull] Action<string> logAction, [NotNull] string message)
			{
				LogAction = logAction;
				Message = message;
			}
		}
	}
}
