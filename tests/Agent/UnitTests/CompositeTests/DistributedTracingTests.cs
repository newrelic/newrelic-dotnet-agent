// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.TestUtilities;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.DistributedTracing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Segments;

namespace CompositeTests
{
    public class DistributedTracingTests
    {
        private const string HeaderName = "newrelic";

        private static CompositeTestAgent _compositeTestAgent;

        private IAgent _agent;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _agent = _compositeTestAgent.GetAgent();
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [Test]
        public void CreatedTransactionIdTraceIdSpanIdShouldBeLowerCase()
        {
            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

            var transaction = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: false);


            var segment = _agent.StartTransactionSegmentOrThrow("segment");
            segment.End();
            transaction.End();

            _compositeTestAgent.Harvest();

            foreach (var span in _compositeTestAgent.SpanEvents)
            {
                var intrinsicAttributes = span.IntrinsicAttributes();
                Assert.IsTrue(IsLowerCase(intrinsicAttributes["traceId"].ToString()));
                Assert.IsTrue(IsLowerCase(intrinsicAttributes["transactionId"].ToString()));
            }

            foreach (TransactionEventWireModel tx in _compositeTestAgent.TransactionEvents)
            {
                Assert.IsTrue(IsLowerCase(tx.IntrinsicAttributes()["guid"].ToString()));
                Assert.IsTrue(IsLowerCase(tx.IntrinsicAttributes()["traceId"].ToString()));
            }
        }

        [Test]
        public void DatastoreSpanEvent_NullCommandTextDoesNotGenerateStatementAttribute()
        {
            var testHostName = "myHost";
            var testPort = "myPort";
            var testDBName = "myDatabase";

            _compositeTestAgent.ServerConfiguration.TrustedAccountKey = "33";
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(NewRelicHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            var segment = _agent.StartDatastoreRequestSegmentOrThrow(null, DatastoreVendor.MSSQL, null, null, null, testHostName, testPort, testDBName);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var spanEvents = _compositeTestAgent.SpanEvents;

            Assert.AreEqual(2, spanEvents.Count);

            // The faux span we create to contain the actual spans.
            var rootSpan = spanEvents.FirstOrDefault(span => span.IntrinsicAttributes().ContainsKey("nr.entryPoint"));

            // The span created from the segment at the top of the test.
            var actualSpan = spanEvents.FirstOrDefault(span => !span.IntrinsicAttributes().ContainsKey("nr.entryPoint"));

            //Test that the information we get on our spans matches the info that we added to the request.
            TestPayloadInfoMatchesSpanInfo(Payload, rootSpan, actualSpan, "datastore");

            var agentAttributes = actualSpan.AgentAttributes();

            //The specific test
            Assert.IsFalse(agentAttributes.ContainsKey("db.statement"));
            Assert.AreEqual(testDBName, agentAttributes["db.instance"]);
            Assert.AreEqual(testHostName, agentAttributes["peer.hostname"]);
            Assert.AreEqual($"{testHostName}:{testPort}", agentAttributes["peer.address"]);
        }

        [Test]
        public void DatastoreSpanEvent_NotNullCommandTextGeneratesStatementAttribute()
        {
            var testHostName = "myHost";
            var testPort = "myPort";
            var testDBName = "myDatabase";
            var testCommand = "myStatement";

            _compositeTestAgent.ServerConfiguration.TrustedAccountKey = "33";
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(NewRelicHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            var segment = _agent.StartDatastoreRequestSegmentOrThrow(null, DatastoreVendor.MSSQL, null, testCommand, null, testHostName, testPort, testDBName);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var spanEvents = _compositeTestAgent.SpanEvents;

            Assert.AreEqual(2, spanEvents.Count);

            // The faux span we create to contain the actual spans.
            var rootSpan = spanEvents.FirstOrDefault(span => span.IntrinsicAttributes().ContainsKey("nr.entryPoint"));

            // The span created from the segment at the top of the test.
            var actualSpan = spanEvents.FirstOrDefault(span => !span.IntrinsicAttributes().ContainsKey("nr.entryPoint"));

            // Test the spans match the payload info from which they were created. 
            TestPayloadInfoMatchesSpanInfo(Payload, rootSpan, actualSpan, "datastore");

            var agentAttributes = actualSpan.AgentAttributes();

            //The specific test
            Assert.AreEqual(testCommand, agentAttributes["db.statement"]);
            Assert.AreEqual(testDBName, agentAttributes["db.instance"]);
            Assert.AreEqual(testHostName, agentAttributes["peer.hostname"]);
            Assert.AreEqual($"{testHostName}:{testPort}", agentAttributes["peer.address"]);
        }

        [Test]
        public void MessageBrokerSegmentResultsInSpanEventCategoryOfGeneric()
        {
            var vendorName = "RabbitMQ";
            var routingKey = "queueName"; //TOPIC has a . QUEUE no .

            _compositeTestAgent.ServerConfiguration.TrustedAccountKey = "33";
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                destinationType: MessageBrokerDestinationType.Queue,
                brokerVendorName: vendorName,
                destination: routingKey);

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(NewRelicHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            var segment = _agent.StartMessageBrokerSegmentOrThrow(vendorName, MessageBrokerDestinationType.Queue,
                routingKey, MessageBrokerAction.Consume);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var spanEvents = _compositeTestAgent.SpanEvents;

            Assert.AreEqual(2, spanEvents.Count);

            // The faux span we create to contain the actual spans.
            var rootSpan = spanEvents.FirstOrDefault(span => span.IntrinsicAttributes().ContainsKey("nr.entryPoint"));

            Assert.NotNull(rootSpan);

            var rootSpanIntrinsicAttributes = rootSpan.IntrinsicAttributes();

            Assert.AreEqual(Payload.TraceId, (string)rootSpanIntrinsicAttributes["traceId"]);
            Assert.AreEqual(Payload.Priority, (double)rootSpanIntrinsicAttributes["priority"]);
            Assert.AreEqual(Payload.Sampled, (bool)rootSpanIntrinsicAttributes["sampled"]);
            Assert.AreEqual(Payload.Guid, (string)rootSpanIntrinsicAttributes["parentId"]);
            Assert.AreEqual("generic", (string)rootSpanIntrinsicAttributes["category"]);
            Assert.True((bool)rootSpanIntrinsicAttributes["nr.entryPoint"]);

            // The span created from the segment at the top of the test.
            var actualSpan = spanEvents.FirstOrDefault(span => !span.IntrinsicAttributes().ContainsKey("nr.entryPoint"));

            Assert.NotNull(actualSpan);

            var actualSpanIntrinsicAttributes = actualSpan.IntrinsicAttributes();

            Assert.AreEqual(Payload.TraceId, (string)actualSpanIntrinsicAttributes["traceId"]);
            Assert.AreEqual(Payload.Priority, (double)actualSpanIntrinsicAttributes["priority"]);
            Assert.AreEqual(Payload.Sampled, (bool)actualSpanIntrinsicAttributes["sampled"]);
            Assert.AreEqual((string)rootSpanIntrinsicAttributes["guid"], (string)actualSpanIntrinsicAttributes["parentId"]);
            Assert.AreEqual((string)rootSpanIntrinsicAttributes["transactionId"], (string)actualSpanIntrinsicAttributes["transactionId"]);
            Assert.AreEqual("generic", (string)actualSpanIntrinsicAttributes["category"]);
        }

        [Test]
        public void ExternalHttpSpanEvent_HasExpectedAttributes()
        {
            var url = "http://127.0.0.2:123/Fake/Url";
            var method = "POST";

            _compositeTestAgent.ServerConfiguration.TrustedAccountKey = "33";
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(NewRelicHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            var segment = _agent.StartExternalRequestSegmentOrThrow(new Uri(url), method);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var spanEvents = _compositeTestAgent.SpanEvents;

            Assert.AreEqual(2, spanEvents.Count);

            // The faux span we create to contain the actual spans.
            var rootSpan = spanEvents.FirstOrDefault(span => span.IntrinsicAttributes().ContainsKey("nr.entryPoint"));

            // The span created from the segment at the top of the test.
            var actualSpan = spanEvents.FirstOrDefault(span => !span.IntrinsicAttributes().ContainsKey("nr.entryPoint"));

            //Test that the information we get on our spans matches the info that we added to the request.
            TestPayloadInfoMatchesSpanInfo(Payload, rootSpan, actualSpan, "http");

            var agentAttributes = actualSpan.AgentAttributes();
            var intrinsicAttributes = actualSpan.IntrinsicAttributes();

            //The specific test
            Assert.AreEqual(url, agentAttributes["http.url"]);
            Assert.AreEqual(method, agentAttributes["http.request.method"]);
            Assert.AreEqual("127.0.0.2", intrinsicAttributes["server.address"]);
            Assert.AreEqual(123, intrinsicAttributes["server.port"]);
        }

        private static Dictionary<string, string> NewRelicHeaders
        {
            get
            {
                var headers = new Dictionary<string, string>();

                var encodedPayload = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(Payload);
                headers.Add(HeaderName, encodedPayload);

                return headers;
            }
        }

        private static DistributedTracePayload Payload => new DistributedTracePayload
        {
            Type = "App",
            AccountId = "33",
            AppId = "2827902",
            TraceId = "d6b4ba0c3a712ca",
            Priority = 1.1f,
            Sampled = true,
            Timestamp = DateTime.UtcNow,
            Guid = "7d3efb1b173fecfa",
            TrustKey = "33",
            TransactionId = "e8b91a159289ff74"
        };

        private static void TestPayloadInfoMatchesSpanInfo(DistributedTracePayload payload, ISpanEventWireModel rootSpan, ISpanEventWireModel actualSpan, string actualCategory)
        {
            Assert.NotNull(rootSpan);

            var rootSpanIntrinsicAttributes = rootSpan.IntrinsicAttributes();

            Assert.IsTrue(AttributeComparer.IsEqualTo(payload.TraceId, rootSpanIntrinsicAttributes["traceId"]));
            Assert.IsTrue(AttributeComparer.IsEqualTo(payload.Priority, rootSpanIntrinsicAttributes["priority"]));
            Assert.IsTrue(AttributeComparer.IsEqualTo(payload.Sampled, rootSpanIntrinsicAttributes["sampled"]));
            Assert.IsTrue(AttributeComparer.IsEqualTo(payload.Guid, rootSpanIntrinsicAttributes["parentId"]));
            Assert.IsTrue(AttributeComparer.IsEqualTo("generic", rootSpanIntrinsicAttributes["category"]));
            Assert.IsTrue(AttributeComparer.IsEqualTo(true, rootSpanIntrinsicAttributes["nr.entryPoint"]));

            Assert.NotNull(actualSpan);

            var actualSpanIntrinsicAttributes = actualSpan.IntrinsicAttributes();

            Assert.IsTrue(AttributeComparer.IsEqualTo(payload.TraceId, actualSpanIntrinsicAttributes["traceId"]));
            Assert.IsTrue(AttributeComparer.IsEqualTo(payload.Priority, actualSpanIntrinsicAttributes["priority"]));
            Assert.IsTrue(AttributeComparer.IsEqualTo(payload.Sampled, actualSpanIntrinsicAttributes["sampled"]));
            Assert.IsTrue(AttributeComparer.IsEqualTo(rootSpanIntrinsicAttributes["guid"], actualSpanIntrinsicAttributes["parentId"]));
            Assert.IsTrue(AttributeComparer.IsEqualTo(rootSpanIntrinsicAttributes["transactionId"], actualSpanIntrinsicAttributes["transactionId"]));
            Assert.IsTrue(AttributeComparer.IsEqualTo(actualCategory, actualSpanIntrinsicAttributes["category"]));
        }

        private bool IsLowerCase(string id)
        {
            return id.Equals(id.ToLowerInvariant());
        }
    }
}
