/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.DataTransport
{
    public class SpanStreamingService : DataStreamingService<Span, RecordStatus>
    {
        public SpanStreamingService(IGrpcWrapper<Span, RecordStatus> grpcWrapper, IDelayer delayer, IConfigurationService configSvc, IAgentHealthReporter agentHealthReporter, IAgentTimerService agentTimerService)
            : base(grpcWrapper, delayer, configSvc, agentHealthReporter, agentTimerService)
        {
        }

        protected override string EndpointHostConfigValue => _configuration?.InfiniteTracingTraceObserverHost;
        protected override string EndpointPortConfigValue => _configuration?.InfiniteTracingTraceObserverPort;
        protected override string EndpointSslConfigValue => _configuration?.InfiniteTracingTraceObserverSsl;
        protected override float? EndpointTestFlakyConfigValue => _configuration?.InfiniteTracingTraceObserverTestFlaky;
        protected override int? EndpointTestDelayMsConfigValue => _configuration?.InfiniteTracingTraceObserverTestDelayMs;

        protected override void HandleServerResponse(RecordStatus responseModel, int consumerId)
        {
            if (responseModel.MessagesSeen == 0)
            {
                return;
            }

            LogMessage(LogLevel.Finest, consumerId, $"Received gRPC Server response: {responseModel.MessagesSeen}");

            RecordReceived(responseModel.MessagesSeen);
                
        }

        private void RecordReceived(ulong countItems)
        {
            _agentHealthReporter.ReportInfiniteTracingSpanEventsReceived(countItems);
        }

        protected override void RecordSuccessfulSend()
        {
            _agentHealthReporter.ReportInfiniteTracingSpanEventsSent();
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
    }

}
