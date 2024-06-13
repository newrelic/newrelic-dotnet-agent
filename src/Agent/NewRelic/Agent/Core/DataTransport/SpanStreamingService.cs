// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Core.SharedInterfaces;

namespace NewRelic.Agent.Core.DataTransport
{
    public class SpanStreamingService : DataStreamingService<Span, SpanBatch, RecordStatus>
    {
        public SpanStreamingService(IGrpcWrapper<SpanBatch, RecordStatus> grpcWrapper, IDelayer delayer, IConfigurationService configSvc, IAgentHealthReporter agentHealthReporter, IAgentTimerService agentTimerService, IEnvironment environment)
            : base(grpcWrapper, delayer, configSvc, agentHealthReporter, agentTimerService, environment)
        {
        }

        protected override string EndpointHostConfigValue => _configuration?.InfiniteTracingTraceObserverHost;
        protected override string EndpointPortConfigValue => _configuration?.InfiniteTracingTraceObserverPort;
        protected override string EndpointSslConfigValue => _configuration?.InfiniteTracingTraceObserverSsl;
        protected override float? EndpointTestFlakyConfigValue => _configuration?.InfiniteTracingTraceObserverTestFlaky;
        protected override int? EndpointTestFlakyCodeConfigValue => _configuration?.InfiniteTracingTraceObserverTestFlakyCode;
        protected override int? EndpointTestDelayMsConfigValue => _configuration?.InfiniteTracingTraceObserverTestDelayMs;
        public override int BatchSizeConfigValue => (_configuration?.InfiniteTracingBatchSizeSpans).GetValueOrDefault(0);

        protected override void HandleServerResponse(RecordStatus responseModel, int consumerId)
        {
            LogMessage(LogLevel.Finest, consumerId, $"Received gRPC Server response messages: {responseModel.MessagesSeen}");

            RecordReceived(responseModel.MessagesSeen);

        }

        private void RecordReceived(ulong countItems)
        {
            _agentHealthReporter.ReportInfiniteTracingSpanEventsReceived(countItems);
        }

        protected override void RecordSuccessfulSend(int countItems)
        {
            _agentHealthReporter.ReportInfiniteTracingSpanEventsSent(countItems);
        }

        protected override void RecordGrpcError(string status)
        {
            _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(status);
        }

        protected override void RecordResponseError()
        {
            _agentHealthReporter.ReportInfiniteTracingSpanResponseError();
        }

        protected override void RecordSendTimeout()
        {
            _agentHealthReporter.ReportInfiniteTracingSpanGrpcTimeout();
        }

        protected override SpanBatch CreateBatch(IList<Span> items)
        {
            var batch = new SpanBatch();
            batch.Spans.AddRange(items);
            return batch;
        }
    }
}
