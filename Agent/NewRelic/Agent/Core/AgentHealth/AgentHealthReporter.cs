using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Collections;
using NewRelic.Core.Logging;
using System;
using System.Collections.Generic;
using System.Net;

namespace NewRelic.Agent.Core.AgentHealth
{
	public class AgentHealthReporter : DisposableService, IAgentHealthReporter, IOutOfBandMetricSource
	{
		private static readonly TimeSpan _timeBetweenExecutions = TimeSpan.FromMinutes(1);

		private readonly IMetricBuilder _metricBuilder;
		private readonly IScheduler _scheduler;
		private readonly IList<RecurringLogData> _recurringLogDatas = new ConcurrentList<RecurringLogData>();
		private readonly IDictionary<AgentHealthEvent, InterlockedCounter> _agentHealthEventCounters = new Dictionary<AgentHealthEvent, InterlockedCounter>();

		private PublishMetricDelegate _publishMetricDelegate;
		private InterlockedCounter _payloadCreateSuccessCounter;
		private InterlockedCounter _payloadAcceptSuccessCounter;

		public AgentHealthReporter(IMetricBuilder metricBuilder, IScheduler scheduler)
		{
			_metricBuilder = metricBuilder;
			_scheduler = scheduler;
			_scheduler.ExecuteEvery(LogRecurringLogs, _timeBetweenExecutions);
			var agentHealthEvents = Enum.GetValues(typeof(AgentHealthEvent)) as AgentHealthEvent[];
			foreach(var agentHealthEvent in agentHealthEvents)
			{
				_agentHealthEventCounters[agentHealthEvent] = new InterlockedCounter();
			}

			_payloadCreateSuccessCounter = new InterlockedCounter();
			_payloadAcceptSuccessCounter = new InterlockedCounter();
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
				if (counter.Value != null && counter.Value.Value > 0)
				{
					var agentHealthEvent = counter.Key;
					var timesOccured = counter.Value.Exchange(0);
					Log.Info($"Event {agentHealthEvent} has occurred {timesOccured} times in the last {_timeBetweenExecutions.TotalSeconds} seconds");
				}
			}
		}

		public void ReportSupportabilityCountMetric(string metricName, int count = 1)
		{
			var metric = _metricBuilder.TryBuildSupportabilityCountMetric(metricName, count);
			TrySend(metric);
		}

		public void ReportDotnetVersion()
		{
#if NET45
			var metric = _metricBuilder.TryBuildDotnetFrameworkVersionMetric(AgentInstallConfiguration.DotnetFrameworkVersion);
#else
			var metric = _metricBuilder.TryBuildDotnetCoreVersionMetric(AgentInstallConfiguration.DotnetCoreVersion);
#endif
			TrySend(metric);
		}

		public void ReportAgentVersion(string agentVersion, string hostName)
		{
			TrySend(_metricBuilder.TryBuildAgentVersionMetric(agentVersion));

			// avoiding call to GetHostName. If this supportability metrics is being used
			// this can be resurrected. But GetHostName calls can then be cached. 
			//TrySend(_metricBuilder.TryBuildAgentVersionByHostMetric(hostName, agentVersion));
		}
		
		public void ReportLibraryVersion(string assemblyName, string assemblyVersion)
		{
			TrySend(_metricBuilder.TryBuildLibraryVersionMetric(assemblyName, assemblyVersion));
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

		public void ReportAgentInfo()
		{
			if(AgentInstallConfiguration.AgentInfo == null)
			{
				TrySend(_metricBuilder.TryBuildInstallTypeMetric("Unknown"));
				return;
			}

			if (AgentInstallConfiguration.AgentInfo.AzureSiteExtension)
			{
				TrySend(_metricBuilder.TryBuildInstallTypeMetric((AgentInstallConfiguration.AgentInfo.InstallType ?? "Unknown") + "SiteExtension"));
			}
			else
			{
				TrySend(_metricBuilder.TryBuildInstallTypeMetric(AgentInstallConfiguration.AgentInfo.InstallType ?? "Unknown"));
			}
		}

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

			foreach (var metric in metrics)
			{
				TrySend(metric);
			}

			Log.Error($"Wrapper {wrapperName} is being disabled for {method.MethodName} due to too many consecutive exceptions. All other methods using this wrapper will continue to be instrumented. This will reduce the functionality of the agent until the agent is restarted.");
			_recurringLogDatas.Add(new RecurringLogData(Log.Debug, $"Wrapper {wrapperName} was disabled for {method.MethodName} at {DateTime.Now} due to too many consecutive exceptions. All other methods using this wrapper will continue to be instrumented. This will reduce the functionality of the agent until the agent is restarted."));
		}

		public void ReportIfHostIsLinuxOs()
		{
#if NETSTANDARD2_0

			bool isLinux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
			var metric =_metricBuilder.TryBuildLinuxOsMetric(isLinux);
			TrySend(metric);
#endif
		}

		public void ReportBootIdError() => TrySend(_metricBuilder.TryBuildBootIdError());

		public void ReportKubernetesUtilizationError() => TrySend(_metricBuilder.TryBuildKubernetesUsabilityError());

		public void ReportAwsUtilizationError() => TrySend(_metricBuilder.TryBuildAwsUsabilityError());

		public void ReportAzureUtilizationError() => TrySend(_metricBuilder.TryBuildAzureUsabilityError());

		public void ReportPcfUtilizationError() => TrySend(_metricBuilder.TryBuildPcfUsabilityError());

		public void ReportGcpUtilizationError() => TrySend(_metricBuilder.TryBuildGcpUsabilityError());

		#region DistributedTrace

		/// <summary>Incremented when AcceptDistributedTracePayload was called successfully</summary>
		public void ReportSupportabilityDistributedTraceAcceptPayloadSuccess()
		{
			_payloadAcceptSuccessCounter.Increment();
		}

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
			TrySend(_metricBuilder.TryBuildAcceptPayloadIgnoredUntrustedAccount());

		/// <summary>Incremented when CreateDistributedTracePayload was called successfully</summary>
		public void ReportSupportabilityDistributedTraceCreatePayloadSuccess()
		{ 
			_payloadCreateSuccessCounter.Increment();
		}

		/// <summary>Created when CreateDistributedTracePayload had a generic exception</summary>
		public void ReportSupportabilityDistributedTraceCreatePayloadException() =>
			TrySend(_metricBuilder.TryBuildCreatePayloadException);

		/// <summary>Limits Collect calls to once per harvest per metric.</summary>
		public void CollectDistributedTraceSuccessMetrics()
		{
			if(TryGetCount(_payloadCreateSuccessCounter, out var createCount))
			{
				TrySend(_metricBuilder.TryBuildCreatePayloadSuccess(createCount));
			}

			if (TryGetCount(_payloadAcceptSuccessCounter, out var acceptCount))
			{
				TrySend(_metricBuilder.TryBuildAcceptPayloadSuccess(acceptCount));
			}

			bool TryGetCount(InterlockedCounter counter, out int metricCount)
			{
				metricCount = 0;
				if (counter.Value > 0)
				{
					metricCount = counter.Exchange(0);
					return true;
				}

				return false;
			}
		}

		#endregion DistributedTrace

		#region Span 
		
		public void ReportSpanEventCollected(int count) => TrySend(_metricBuilder.TryBuildSpanEventsSeenMetric(count));

		public void ReportSpanEventsSent(int count) => TrySend(_metricBuilder.TryBuildSpanEventsSentMetric(count));

		#endregion Span 

		public void ReportAgentTimingMetric(string suffix, TimeSpan time)
		{
			var metric = _metricBuilder.TryBuildAgentTimingMetric(suffix, time);
			TrySend(metric);
		}

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

		public void ReportSupportabilityPayloadsDroppeDueToMaxPayloadSizeLimit(string endpoint)
		{
			TrySend(_metricBuilder.TryBuildSupportabilityPayloadsDroppedDueToMaxPayloadLimit(endpoint));
		}

		public void RegisterPublishMetricHandler(PublishMetricDelegate publishMetricDelegate)
		{
			if (_publishMetricDelegate != null)
			{
				Log.Warn("Existing PublishMetricDelegate registration being overwritten.");
			}

			_publishMetricDelegate = publishMetricDelegate;
		}

		private void TrySend(MetricWireModel metric)
		{
			if (metric == null)
			{
				return;
			}

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
			public readonly Action<string> LogAction;
			public readonly string Message;

			public RecurringLogData(Action<string> logAction, string message)
			{
				LogAction = logAction;
				Message = message;
			}
		}
	}
}
