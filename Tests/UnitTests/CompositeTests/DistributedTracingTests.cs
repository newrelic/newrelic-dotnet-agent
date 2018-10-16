using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace CompositeTests
{
	public class DistributedTracingTests
	{
		private const string HeaderName = "newrelic";
		private const string TransportType = "HTTP";

		private static CompositeTestAgent _compositeTestAgent;

		private IAgentWrapperApi _agentWrapperApi;

		[SetUp]
		public void SetUp()
		{
			_compositeTestAgent = new CompositeTestAgent();
			_agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();
		}

		[TearDown]
		public static void TearDown()
		{
			_compositeTestAgent.Dispose();
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

			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				_agentWrapperApi.ProcessInboundRequest(NewRelicHeaders, TransportType);

				var segment = _agentWrapperApi.StartDatastoreRequestSegmentOrThrow(null, DatastoreVendor.MSSQL, null, null, null,testHostName,testPort,testDBName);
				segment.End();
			}

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
			Assert.IsFalse(actualSpan.IntrinsicAttributes.ContainsKey("db.statement"));
			Assert.AreEqual(testDBName, actualSpan.IntrinsicAttributes["db.instance"]);
			Assert.AreEqual(testHostName, actualSpan.IntrinsicAttributes["peer.hostname"]);
			Assert.AreEqual($"{testHostName}:{testPort}", actualSpan.IntrinsicAttributes["peer.address"]);
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

			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				_agentWrapperApi.ProcessInboundRequest(NewRelicHeaders, TransportType);

				var segment = _agentWrapperApi.StartDatastoreRequestSegmentOrThrow(null, DatastoreVendor.MSSQL, null, testCommand, null, testHostName, testPort, testDBName);
				segment.End();
			}

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
			Assert.AreEqual(testCommand, actualSpan.IntrinsicAttributes["db.statement"]);
			Assert.AreEqual(testDBName, actualSpan.IntrinsicAttributes["db.instance"]);
			Assert.AreEqual(testHostName, actualSpan.IntrinsicAttributes["peer.hostname"]);
			Assert.AreEqual($"{testHostName}:{testPort}", actualSpan.IntrinsicAttributes["peer.address"]);
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
			TraceId = "d6b4ba0c3a712ca".ToUpperInvariant(),
			Priority = 1.1f,
			Sampled = true,
			Timestamp = DateTime.UtcNow,
			Guid = "7d3efb1b173fecfa".ToUpperInvariant(),
			TrustKey = "33",
			TransactionId = "e8b91a159289ff74".ToUpperInvariant()
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

	}
}
