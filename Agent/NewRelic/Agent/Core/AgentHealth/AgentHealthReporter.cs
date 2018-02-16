using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Collections;

namespace NewRelic.Agent.Core.AgentHealth
{
	public interface IAgentHealthReporter
	{
		void ReportAgentVersion([NotNull] String agentVersion, [NotNull] String hostName);
		void ReportTransactionEventReservoirResized(UInt32 newSize);
		void ReportTransactionEventCollected();
		void ReportTransactionEventsRecollected(Int32 count);
		void ReportTransactionEventsSent(Int32 count);
		void ReportCustomEventReservoirResized(UInt32 newSize);
		void ReportCustomEventCollected();
		void ReportCustomEventsRecollected(Int32 count);
		void ReportCustomEventsSent(Int32 count);
		void ReportErrorTraceCollected();
		void ReportErrorTracesRecollected(Int32 count);
		void ReportErrorTracesSent(Int32 count);
		void ReportErrorEventSeen();
		void ReportErrorEventsSent(Int32 count);
		void ReportSqlTracesRecollected(Int32 count);
		void ReportSqlTracesSent(Int32 count);

		void ReportTransactionGarbageCollected(TransactionMetricName transactionMetricName, [NotNull] String lastStartedSegmentName, [NotNull] String lastFinishedSegmentName);

		void ReportWrapperShutdown([NotNull] IWrapper wrapper, [NotNull] Method method);
		void ReportAgentApiMethodCalled([NotNull] String methodName);
		void ReportIfHostIsLinuxOs();
		void ReportBootIdError();
		void ReportAwsUtilizationError();
		void ReportAzureUtilizationError();
		void ReportPcfUtilizationError();
		void ReportGcpUtilizationError();
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
				if ( data != null)
				{
					data.LogAction(data.Message);
				}
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

		public void ReportAgentVersion(String agentVersion, String hostName)
		{
			var metrics = new[]
			{
				_metricBuilder.TryBuildAgentVersionMetric(agentVersion),
				_metricBuilder.TryBuildAgentVersionByHostMetric(hostName, agentVersion)
			};

			foreach(var metric in metrics)
			{
				TrySend(metric);
			}
		}

		#region TransactionEvents

		public void ReportTransactionEventReservoirResized(UInt32 newSize)
		{
			var metrics = new[]
			{
				_metricBuilder.TryBuildTransactionEventReservoirResizedMetric(),
			};

			foreach(var metric in metrics)
			{
				TrySend(metric);
			}

			Log.Warn("Resizing transaction event reservoir to " + newSize + " events.");
		}

		public void ReportTransactionEventCollected()
		{
			var metrics = new[]
			{
				_metricBuilder.TryBuildTransactionEventsCollectedMetric(),

				// Note: this metric is REQUIRED by APM (see https://source.datanerd.us/agents/agent-specs/pull/84)
				_metricBuilder.TryBuildTransactionEventsSeenMetric()
			};

			foreach (var metric in metrics)
			{
				TrySend(metric);
			}
		}

		public void ReportTransactionEventsRecollected(Int32 count)
		{
			var metrics = new[]
			{
				_metricBuilder.TryBuildTransactionEventsRecollectedMetric(count)
			};

			foreach (var metric in metrics)
			{
				TrySend(metric);
			}
		}

		public void ReportTransactionEventsSent(Int32 count)
		{
			var metrics = new[]
			{
				// Note: this metric is REQUIRED by APM (see https://source.datanerd.us/agents/agent-specs/pull/84)
				_metricBuilder.TryBuildTransactionEventsSentMetric(count),
			};

			foreach (var metric in metrics)
			{
				TrySend(metric);
			}
		}

		#endregion TransactionEvents

		#region CustomEvents

		public void ReportCustomEventReservoirResized(UInt32 newSize)
		{
			var metrics = new[]
			{
				_metricBuilder.TryBuildCustomEventReservoirResizedMetric(),
			};

			foreach (var metric in metrics)
			{
				TrySend(metric);
			}

			Log.Warn("Resizing custom event reservoir to " + newSize + " events.");
		}

		public void ReportCustomEventCollected()
		{
			var metrics = new[]
			{
				_metricBuilder.TryBuildCustomEventsCollectedMetric(),
				
				// Note: Though not required by APM like the transaction event supportability metrics, this metric should still be created to maintain consistency
				_metricBuilder.TryBuildCustomEventsSeenMetric()
			};

			foreach (var metric in metrics)
			{
				TrySend(metric);
			}
		}

		public void ReportCustomEventsRecollected(Int32 count)
		{
			var metrics = new[]
			{
				_metricBuilder.TryBuildCustomEventsRecollectedMetric(count)
			};

			foreach (var metric in metrics)
			{
				TrySend(metric);
			}
		}

		public void ReportCustomEventsSent(Int32 count)
		{
			var metrics = new[]
			{
				// Note: Though not required by APM like the transaction event supportability metrics, this metric should still be created to maintain consistency
				_metricBuilder.TryBuildCustomEventsSentMetric(count),
			};

			foreach (var metric in metrics)
			{
				TrySend(metric);
			}
		}

		#endregion CustomEvents

		#region ErrorTraces

		public void ReportErrorTraceCollected()
		{
			var metrics = new[]
			{
				_metricBuilder.TryBuildErrorTracesCollectedMetric(),
			};

			foreach (var metric in metrics)
			{
				TrySend(metric);
			}
		}

		public void ReportErrorTracesRecollected(Int32 count)
		{
			var metrics = new[]
			{
				_metricBuilder.TryBuildErrorTracesRecollectedMetric(count)
			};

			foreach (var metric in metrics)
			{
				TrySend(metric);
			}
		}

		public void ReportErrorTracesSent(Int32 count)
		{
			var metrics = new[]
			{
				_metricBuilder.TryBuildErrorTracesSentMetric(count),
			};

			foreach (var metric in metrics)
			{
				TrySend(metric);
			}
		}

		#endregion ErrorTraces

		#region ErrorEvents

		public void ReportErrorEventSeen()
		{
			var metrics = new[]
			{
				_metricBuilder.TryBuildErrorEventsSeenMetric(),
			};

			foreach (var metric in metrics)
			{
				TrySend(metric);
			}
		}

		public void ReportErrorEventsSent(Int32 count)
		{
			var metrics = new[]
			{
				_metricBuilder.TryBuildErrorEventsSentMetric(count),
			};

			foreach (var metric in metrics)
			{
				TrySend(metric);
			}
		}
		#endregion ErrorEvents

		#region SqlTraces

		public void ReportSqlTracesRecollected(Int32 count)
		{
			var metrics = new[]
			{
				_metricBuilder.TryBuildSqlTracesRecollectedMetric(count)
			};

			foreach (var metric in metrics)
			{
				TrySend(metric);
			}
		}

		public void ReportSqlTracesSent(Int32 count)
		{
			var metrics = new[]
			{
				_metricBuilder.TryBuildSqlTracesSentMetric(count),
			};

			foreach (var metric in metrics)
			{
				TrySend(metric);
			}
		}

		#endregion ErrorTraces

		public void ReportTransactionGarbageCollected(TransactionMetricName transactionMetricName, String lastStartedSegmentName, String lastFinishedSegmentName)
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

		public void ReportAgentApiMethodCalled(String methodName)
		{
			var metrics = new[]
			{
				_metricBuilder.TryBuildAgentApiMetric(methodName)
			};

			foreach(var metric in metrics)
			{
				TrySend(metric);
			}
		}

		public void ReportIfHostIsLinuxOs()
		{

#if NETSTANDARD2_0

			bool isLinux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
			var metric =_metricBuilder.TryBuildLinuxOsMetric(isLinux);
			TrySend(metric);
#endif
		}

		public void ReportBootIdError()
		{
			var metric = _metricBuilder.TryBuildBootIdError();
			TrySend(metric);
		}

		public void ReportAwsUtilizationError()
		{
			var metric = _metricBuilder.TryBuildAwsUsabilityError();
			TrySend(metric);
		}

		public void ReportAzureUtilizationError()
		{
			var metric = _metricBuilder.TryBuildAzureUsabilityError();
			TrySend(metric);
		}

		public void ReportPcfUtilizationError()
		{
			var metric = _metricBuilder.TryBuildPcfUsabilityError();
			TrySend(metric);
		}

		public void ReportGcpUtilizationError()
		{
			var metric = _metricBuilder.TryBuildGcpUsabilityError();
			TrySend(metric);
		}

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
			public readonly Action<String> LogAction;

			[NotNull]
			public readonly String Message;

			public RecurringLogData([NotNull] Action<String> logAction, [NotNull] String message)
			{
				LogAction = logAction;
				Message = message;
			}
		}
	}
}
