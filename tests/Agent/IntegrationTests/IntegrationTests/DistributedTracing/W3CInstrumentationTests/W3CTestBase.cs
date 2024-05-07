// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Linq;
using Xunit;
using Xunit.Abstractions;
using NewRelic.Agent.IntegrationTestHelpers;
using System.Collections.Generic;
using NewRelic.Testing.Assertions;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;

namespace NewRelic.Agent.IntegrationTests.DistributedTracing.W3CInstrumentationTests
{
    public abstract class W3CTestBase<T> : NewRelicIntegrationTest<T> where T : TracingChainFixture
    {
        protected T _fixture;
        protected const string TestTraceId = "12345678901234567890123456789012";
        protected const string TestTraceParent = "1234567890123456";
        protected const string TestTracingVendors = "rojo,congo";
        private const string TestOtherVendorEntries = "rojo=1,congo=2";
        protected readonly KeyValuePair<string, string>[] Headers = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string> ("traceparent", $"00-{TestTraceId}-{TestTraceParent}-00"),
                new KeyValuePair<string, string> ("tracestate", TestOtherVendorEntries)
            };

        public W3CTestBase(T fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetOrDeleteSpanEventsEnabled(true);
                    configModifier.SetLogLevel("debug");

                    var environmentVariables = new Dictionary<string, string>();

                    _fixture.ReceiverApplication = _fixture.SetupReceiverApplication(isDistributedTracing: true, isWebApplication: _fixture is OwinTracingChainFixture ? false : true);
                    _fixture.ReceiverApplication.Start(string.Empty, environmentVariables, captureStandardOutput: true);
                });
        }

        private TransactionEvent _senderAppTxEvent;
        protected TransactionEvent SenderAppTxEvent => _senderAppTxEvent ?? (_senderAppTxEvent = _fixture.AgentLog.GetTransactionEvents().FirstOrDefault());

        private TransactionEvent _receiverAppTxEvent;
        protected TransactionEvent ReceiverAppTxEvent => _receiverAppTxEvent ?? (_receiverAppTxEvent = _fixture.ReceiverAppAgentLog.GetTransactionEvents().FirstOrDefault());

        private List<SpanEvent> _senderAppSpanEvents;
        protected List<SpanEvent> SenderAppSpanEvents => _senderAppSpanEvents ?? (_senderAppSpanEvents = _fixture.AgentLog.GetSpanEvents().ToList());

        private SpanEvent[] _receiverAppSpanEvents;
        protected SpanEvent[] ReceiverAppSpanEvents => _receiverAppSpanEvents ?? (_receiverAppSpanEvents = _fixture.ReceiverAppAgentLog.GetSpanEvents().ToArray());

        private Metric[] _senderActualMetrics;
        protected Metric[] SenderActualMetrics => _senderActualMetrics ?? (_senderActualMetrics = _fixture.AgentLog.GetMetrics().ToArray());

        private Metric[] _receiverActualMetrics;
        protected Metric[] ReceiverActualMetrics => _receiverActualMetrics ?? (_receiverActualMetrics = _fixture.ReceiverAppAgentLog.GetMetrics().ToArray());

        [Fact]
        public void TransactionsAttributes()
        {
            Assert.NotNull(SenderAppTxEvent);
            Assert.NotNull(ReceiverAppTxEvent);

            Assert.Equal(SenderAppTxEvent.IntrinsicAttributes["guid"], ReceiverAppTxEvent.IntrinsicAttributes["parentId"]);
        }

        [Fact]
        public void SpansAttributes()
        {
            foreach (var span in SenderAppSpanEvents)
            {
                Assert.Equal(TestTraceId, span.IntrinsicAttributes["traceId"]);
                Assert.True(AttributeComparer.IsEqualTo(SenderAppTxEvent.IntrinsicAttributes["priority"], span.IntrinsicAttributes["priority"]));
            }

            foreach (var span in ReceiverAppSpanEvents)
            {
                Assert.Equal(TestTraceId, span.IntrinsicAttributes["traceId"]);
                Assert.True(AttributeComparer.IsEqualTo(SenderAppTxEvent.IntrinsicAttributes["priority"], span.IntrinsicAttributes["priority"]));
            }
        }

        [Fact]
        public abstract void RootSpanAttributes();

        List<Assertions.ExpectedMetric> SenderExpectedMetrics =
            new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/CreatePayload/Success", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Accept/Success", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Create/Success", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/TraceState/NoNrEntry", callCount = 1 }
            };


        private List<Assertions.ExpectedMetric> GetReceiverExpectedMetrics()
        {
            var accountId = _fixture.AgentLog.GetAccountId();
            var appId = _fixture.AgentLog.GetApplicationId();

            return new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Accept/Success", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"DurationByCaller/App/{accountId}/{appId}/HTTP/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"DurationByCaller/App/{accountId}/{appId}/HTTP/allWeb", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"TransportDuration/App/{accountId}/{appId}/HTTP/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"TransportDuration/App/{accountId}/{appId}/HTTP/allWeb", callCount = 1 }
            };
        }

        List<Assertions.ExpectedMetric> ReceiverUnexpectedMetrics =
            new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Create/Success", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/TraceState/NoNrEntry", callCount = 1 }
            };

        [Fact]
        public void Metrics()
        {
            NrAssert.Multiple(
                () => Assertions.MetricsExist(SenderExpectedMetrics, SenderActualMetrics),
                () => Assertions.MetricsExist(GetReceiverExpectedMetrics(), ReceiverActualMetrics),
                () => Assertions.MetricsDoNotExist(ReceiverUnexpectedMetrics, ReceiverActualMetrics)
            );
        }
    }
}
