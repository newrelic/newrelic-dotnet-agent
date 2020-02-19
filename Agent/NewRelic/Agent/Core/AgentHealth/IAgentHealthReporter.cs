using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System;
using System.Net;

namespace NewRelic.Agent.Core.AgentHealth
{
	public interface IAgentHealthReporter : IOutOfBandMetricSource
	{
		void ReportDotnetVersion();

		void ReportAgentVersion(string agentVersion, string hostName);

		void ReportLibraryVersion(string assemblyName, string assemblyVersion);

		void ReportTransactionEventReservoirResized(int newSize);

		void ReportTransactionEventCollected();

		void ReportTransactionEventsRecollected(int count);

		void ReportTransactionEventsSent(int count);

		void ReportCustomEventReservoirResized(int newSize);

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

		void ReportTransactionGarbageCollected(TransactionMetricName transactionMetricName, string lastStartedSegmentName, string lastFinishedSegmentName);

		void ReportWrapperShutdown(IWrapper wrapper, Method method);

		void ReportIfHostIsLinuxOs();

		void ReportBootIdError();
		void ReportKubernetesUtilizationError();
		void ReportAwsUtilizationError();
		void ReportAzureUtilizationError();
		void ReportPcfUtilizationError();
		void ReportGcpUtilizationError();
		void ReportAgentTimingMetric(string timerName, TimeSpan stopWatchElapsedMilliseconds);

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

		void ReportSpanEventCollected(int count);

		void ReportSpanEventsSent(int count);

		void CollectDistributedTraceSuccessMetrics();

		void ReportSupportabilityPayloadsDroppeDueToMaxPayloadSizeLimit(string endpoint);

		void ReportAgentInfo();

		void ReportSupportabilityCountMetric(string metricName, int count = 1);
	}
}
