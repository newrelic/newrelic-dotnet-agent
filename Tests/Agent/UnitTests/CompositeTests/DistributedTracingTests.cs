using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.DistributedTracing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

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
			_compositeTestAgent.LocalConfiguration.distributedTracing.enabled = true;
			_compositeTestAgent.LocalConfiguration.spanEvents.enabled = true;
			_compositeTestAgent.PushConfiguration();
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

			foreach (SpanEventWireModel span in _compositeTestAgent.SpanEvents)
			{
				Assert.IsTrue(IsLowerCase(span.IntrinsicAttributes["traceId"].ToString()));
				Assert.IsTrue(IsLowerCase(span.IntrinsicAttributes["transactionId"].ToString()));
			}

			foreach (TransactionEventWireModel tx in _compositeTestAgent.TransactionEvents)
			{
				Assert.IsTrue(IsLowerCase(tx.IntrinsicAttributes["guid"].ToString()));
				Assert.IsTrue(IsLowerCase(tx.IntrinsicAttributes["traceId"].ToString()));
			}
		}

		[Test]
		public void DatastoreSpanEvent_NullCommandTextDoesNotGenerateStatementAttribute()
		{
			var testHostName = "myHost";
			var testPort = "myPort";
			var testDBName = "myDatabase";

			_compositeTestAgent.LocalConfiguration.distributedTracing.enabled = true;
			_compositeTestAgent.LocalConfiguration.spanEvents.enabled = true;
			_compositeTestAgent.ServerConfiguration.TrustedAccountKey = "33";
			_compositeTestAgent.PushConfiguration();
			//SpanEvents were not enabled when the aggregators were first started so we need to start them here.
			EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

			var tx = _agent.CreateTransaction(
				isWeb: true,
				category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
				transactionDisplayName: "name",
				doNotTrackAsUnitOfWork: true);
			_agent.ProcessInboundRequest(NewRelicHeaders, TransportType.HTTP);

			var segment = _agent.StartDatastoreRequestSegmentOrThrow(null, DatastoreVendor.MSSQL, null, null, null,testHostName,testPort,testDBName);
			segment.End();
			tx.End();

			_compositeTestAgent.Harvest();

			var spanEvents = _compositeTestAgent.SpanEvents;

			Assert.AreEqual(2, spanEvents.Count);

			// The faux span we create to contain the actual spans.
			var rootSpan = spanEvents.FirstOrDefault(span => span.IntrinsicAttributes.ContainsKey("nr.entryPoint"));
			
			// The span created from the segment at the top of the test.
			var actualSpan = spanEvents.FirstOrDefault(span => !span.IntrinsicAttributes.ContainsKey("nr.entryPoint"));

			//Test that the information we get on our spans matches the info that we added to the request.
			TestPayloadInfoMatchesSpanInfo(Payload, rootSpan, actualSpan);

			//The specific test
			Assert.IsFalse(actualSpan.AgentAttributes.ContainsKey("db.statement"));
			Assert.AreEqual(testDBName, actualSpan.AgentAttributes["db.instance"]);
			Assert.AreEqual(testHostName, actualSpan.AgentAttributes["peer.hostname"]);
			Assert.AreEqual($"{testHostName}:{testPort}", actualSpan.AgentAttributes["peer.address"]);
		}

		[Test]
		public void DatastoreSpanEvent_NotNullCommandTextGeneratesStatementAttribute()
		{
			var testHostName = "myHost";
			var testPort = "myPort";
			var testDBName = "myDatabase";
			var testCommand = "myStatement";


			_compositeTestAgent.LocalConfiguration.distributedTracing.enabled = true;
			_compositeTestAgent.LocalConfiguration.spanEvents.enabled = true;
			_compositeTestAgent.ServerConfiguration.TrustedAccountKey = "33";
			_compositeTestAgent.PushConfiguration();
			//SpanEvents were not enabled when the aggregators were first started so we need to start them here.
			EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

			var tx = _agent.CreateTransaction(
				isWeb: true,
				category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
				transactionDisplayName: "name",
				doNotTrackAsUnitOfWork: true);
			_agent.ProcessInboundRequest(NewRelicHeaders, TransportType.HTTP);

			var segment = _agent.StartDatastoreRequestSegmentOrThrow(null, DatastoreVendor.MSSQL, null, testCommand, null, testHostName, testPort, testDBName);
			segment.End();
			tx.End();

			_compositeTestAgent.Harvest();

			var spanEvents = _compositeTestAgent.SpanEvents;

			Assert.AreEqual(2, spanEvents.Count);

			// The faux span we create to contain the actual spans.
			var rootSpan = spanEvents.FirstOrDefault(span => span.IntrinsicAttributes.ContainsKey("nr.entryPoint"));
			
			// The span created from the segment at the top of the test.
			var actualSpan = spanEvents.FirstOrDefault(span => !span.IntrinsicAttributes.ContainsKey("nr.entryPoint"));

			// Test the spans match the payload info from which they were created. 
			TestPayloadInfoMatchesSpanInfo(Payload, rootSpan, actualSpan);
			
			//The specific test
			Assert.AreEqual(testCommand, actualSpan.AgentAttributes["db.statement"]);
			Assert.AreEqual(testDBName, actualSpan.AgentAttributes["db.instance"]);
			Assert.AreEqual(testHostName, actualSpan.AgentAttributes["peer.hostname"]);
			Assert.AreEqual($"{testHostName}:{testPort}", actualSpan.AgentAttributes["peer.address"]);
		}

		[Test]
		public void MessageBrokerSegmentResultsInSpanEventCategoryOfGeneric()
		{
			var vendorName = "RabbitMQ";
			var routingKey = "queueName"; //TOPIC has a . QUEUE no .

			_compositeTestAgent.LocalConfiguration.distributedTracing.enabled = true;
			_compositeTestAgent.LocalConfiguration.spanEvents.enabled = true;
			_compositeTestAgent.ServerConfiguration.TrustedAccountKey = "33";
			_compositeTestAgent.PushConfiguration();
			//SpanEvents were not enabled when the aggregators were first started so we need to start them here.
			EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

			var tx = _agent.CreateTransaction(
				destinationType: MessageBrokerDestinationType.Queue,
				brokerVendorName: vendorName,
				destination: routingKey);
			_agent.ProcessInboundRequest(NewRelicHeaders, TransportType.HTTP);
			var segment = _agent.StartMessageBrokerSegmentOrThrow(vendorName, MessageBrokerDestinationType.Queue,
				routingKey, MessageBrokerAction.Consume);
			segment.End();
			tx.End();

			_compositeTestAgent.Harvest();

			var spanEvents = _compositeTestAgent.SpanEvents;

			Assert.AreEqual(2, spanEvents.Count);

			// The faux span we create to contain the actual spans.
			var rootSpan = spanEvents.FirstOrDefault(span => span.IntrinsicAttributes.ContainsKey("nr.entryPoint"));

			Assert.NotNull(rootSpan);
			Assert.AreEqual(Payload.TraceId, (string)rootSpan.IntrinsicAttributes["traceId"]);
			Assert.AreEqual(Payload.Priority, (float)rootSpan.IntrinsicAttributes["priority"]);
			Assert.AreEqual(Payload.Sampled, (bool)rootSpan.IntrinsicAttributes["sampled"]);
			Assert.AreEqual(Payload.Guid, (string)rootSpan.IntrinsicAttributes["parentId"]);
			Assert.AreEqual("generic", (string)rootSpan.IntrinsicAttributes["category"]);
			Assert.True((bool)rootSpan.IntrinsicAttributes["nr.entryPoint"]);

			// The span created from the segment at the top of the test.
			var actualSpan = spanEvents.FirstOrDefault(span => !span.IntrinsicAttributes.ContainsKey("nr.entryPoint"));

			Assert.NotNull(actualSpan);
			Assert.AreEqual(Payload.TraceId, (string)actualSpan.IntrinsicAttributes["traceId"]);
			Assert.AreEqual(Payload.Priority, (float)actualSpan.IntrinsicAttributes["priority"]);
			Assert.AreEqual(Payload.Sampled, (bool)actualSpan.IntrinsicAttributes["sampled"]);
			Assert.AreEqual((string)rootSpan.IntrinsicAttributes["guid"], (string)actualSpan.IntrinsicAttributes["parentId"]);
			Assert.AreEqual((string)rootSpan.IntrinsicAttributes["transactionId"], (string)actualSpan.IntrinsicAttributes["transactionId"]);
			Assert.AreEqual("generic", (string)actualSpan.IntrinsicAttributes["category"]);
		}

		private static Dictionary<string, string> NewRelicHeaders
		{
			get
			{
				var headers = new Dictionary<string, string>();

				var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(Payload);
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

		private static void TestPayloadInfoMatchesSpanInfo(DistributedTracePayload payload, SpanEventWireModel rootSpan, SpanEventWireModel actualSpan)
		{
			Assert.NotNull(rootSpan);
			Assert.AreEqual(payload.TraceId, (string)rootSpan.IntrinsicAttributes["traceId"]);
			Assert.AreEqual(payload.Priority, (float)rootSpan.IntrinsicAttributes["priority"]);
			Assert.AreEqual(payload.Sampled, (bool)rootSpan.IntrinsicAttributes["sampled"]);
			Assert.AreEqual(payload.Guid, (string)rootSpan.IntrinsicAttributes["parentId"]);
			Assert.AreEqual("generic", (string)rootSpan.IntrinsicAttributes["category"]);
			Assert.True((bool)rootSpan.IntrinsicAttributes["nr.entryPoint"]);

			Assert.NotNull(actualSpan);
			Assert.AreEqual(payload.TraceId, (string)actualSpan.IntrinsicAttributes["traceId"]);
			Assert.AreEqual(payload.Priority, (float)actualSpan.IntrinsicAttributes["priority"]);
			Assert.AreEqual(payload.Sampled, (bool)actualSpan.IntrinsicAttributes["sampled"]);
			Assert.AreEqual((string)rootSpan.IntrinsicAttributes["guid"], (string)actualSpan.IntrinsicAttributes["parentId"]);
			Assert.AreEqual((string)rootSpan.IntrinsicAttributes["transactionId"], (string)actualSpan.IntrinsicAttributes["transactionId"]);
			Assert.AreEqual("datastore", (string)actualSpan.IntrinsicAttributes["category"]);
		}
		
		private bool IsLowerCase(string id)
		{
			return id.Equals(id.ToLowerInvariant());
		}
	}
}
