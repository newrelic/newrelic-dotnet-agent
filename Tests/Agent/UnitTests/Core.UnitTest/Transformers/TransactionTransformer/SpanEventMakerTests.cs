using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	[TestFixture]
	public class SpanEventMakerTests
	{
		private const float Priority = 0.5f;
		private const string MethodCallType = "type";
		private const string MethodCallMethod = "method";
		private const string SegmentName = "test";
		private const string DistributedTraceTraceId = "distributedTraceTraceId";
		private const string DistributedTraceGuid = "distributedTraceGuid";
		private const string GenericCategory = "generic";
		private const string DatastoreCategory = "datastore";
		private const string HttpCategory = "http";
		private const string ShortQuery = "Select * from users where ssn = 433871122";
		private const string ShortObfuscatedQuery = "Select * from users where ssn = ?";
		// over 2000 bytes long, 2000 bytes ends on last '@'  not a real query to avoid obfuscation
		private const string Query2015Characters1ByteEach = "#########################"
			+ "########################################################################################################################################################################################################"
			+ "########################################################################################################################################################################################################"
			+ "########################################################################################################################################################################################################"
			+ "########################################################################################################################################################################################################"
			+ "########################################################################################################################################################################################################"
			+ "########################################################################################################################################################################################################"
			+ "########################################################################################################################################################################################################"
			+ "########################################################################################################################################################################################################"
			+ "########################################################################################################################################################################################################"
			+ "###########################################################################################################################################################################@@@@ %%%%%%%%%%";
		// Includes the three ...
		private const string TruncatedQuery2015Characters1ByteEach = "#########################"
			+ "########################################################################################################################################################################################################"
			+ "########################################################################################################################################################################################################"
			+ "########################################################################################################################################################################################################"
			+ "########################################################################################################################################################################################################"
			+ "########################################################################################################################################################################################################"
			+ "########################################################################################################################################################################################################"
			+ "########################################################################################################################################################################################################"
			+ "########################################################################################################################################################################################################"
			+ "########################################################################################################################################################################################################"
			+ "###########################################################################################################################################################################@...";
		private const string Query1015Characters2ByteEach = "πππππππππππππππππππππππππ"
			+ "ππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππ"
			+ "ππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππ"
			+ "ππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππ"
			+ "ππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππ"
			+ "πππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππ %%%%%%%%%%";
		private const string TruncatedQuery1015Characters2ByteEach = "πππππππππππππππππππππππππ"
			+ "ππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππ"
			+ "ππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππ"
			+ "ππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππ"
			+ "ππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππ"
			+ "πππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππππ...";
		private const string HttpUri = "http://localhost:80/api/test";
		private const string HttpMethod = "GET";

		private const string TransactionName = "WebTransaction/foo/bar";

		private SpanEventMaker _spanEventMaker;
		private string _transactionGuid;
		private DateTime _startTime;
		private Segment _baseGenericSegment;
		private Segment _childGenericSegment;
		private Segment _baseDatastoreSegment;
		private Segment _baseHttpSegment;
		private ConnectionInfo _connectionInfo;

		[SetUp]
		public void SetUp()
		{
			_spanEventMaker = new SpanEventMaker();
			_transactionGuid = GuidGenerator.GenerateNewRelicGuid();
			_startTime = new DateTime(2018, 7, 18, 7, 0, 0, DateTimeKind.Utc); // unixtime = 1531897200000

			// Generic Segments
			_baseGenericSegment = new TypedSegment<SimpleSegmentData>(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1), new SimpleSegmentData(SegmentName), false);
			_childGenericSegment = new TypedSegment<SimpleSegmentData>(CreateTransactionSegmentState(4, 3, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1), new SimpleSegmentData(SegmentName), false);

			// Datastore Segments
			_connectionInfo = new ConnectionInfo("localhost", "1234", "default", "maininstance");
			var shortSqlStatement = new ParsedSqlStatement(DatastoreVendor.MSSQL, ShortQuery, "select");
			_baseDatastoreSegment = new TypedSegment<DatastoreSegmentData>(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1), new DatastoreSegmentData(shortSqlStatement, ShortQuery, _connectionInfo), false);

			// Http Segments
			_baseHttpSegment = new TypedSegment<ExternalSegmentData>(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1), new ExternalSegmentData(new Uri(HttpUri), HttpMethod), false);
		}

		#region Generic and  General Tests

		[Test]
		public void GetSpanEvent_ReturnsSpanEventPerSegment_ValidateCount()
		{
			// ARRANGE
			var segments = new List<Segment>()
			{
				_baseGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>()),
				_childGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
			};
			var immutableTransaction = BuildTestTransaction(segments, sampled: true, hasIncomingPayload: false);

			// ACT
			var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName);

			// ASSERT
			// +1 is for the faux root segment.
			Assert.AreEqual(segments.Count + 1, spanEvents.Count());
		}

		[Test]
		public void GetSpanEvent_ReturnsSpanEventPerSegment_ValidateChildValues()
		{
			// ARRANGE
			var segments = new List<Segment>()
			{
				_baseGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>()),
				_childGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
			};
			var immutableTransaction = BuildTestTransaction(segments, true, false);

			// ACT
			var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName);
			var spanEvent = spanEvents.ToList()[2]; // look at child span only since it has all the values
			
			// ASSERT
			Assert.AreEqual("Span", (string)spanEvent.IntrinsicAttributes["type"]);
			Assert.AreEqual(DistributedTraceTraceId, (string)spanEvent.IntrinsicAttributes["traceId"]);
			Assert.AreEqual(_childGenericSegment.SpanId, (string)spanEvent.IntrinsicAttributes["guid"]);
			Assert.AreEqual(_baseGenericSegment.SpanId, (string)spanEvent.IntrinsicAttributes["parentId"]);
			Assert.AreEqual(_transactionGuid, (string)spanEvent.IntrinsicAttributes["transactionId"]);
			Assert.AreEqual(true, (bool)spanEvent.IntrinsicAttributes["sampled"]);
			Assert.AreEqual(Priority, (float?)spanEvent.IntrinsicAttributes["priority"]);
			Assert.AreEqual(1531897200001, (long)spanEvent.IntrinsicAttributes["timestamp"]);
			Assert.AreEqual(0.005f, (float?)spanEvent.IntrinsicAttributes["duration"]);
			Assert.AreEqual(SegmentName, (string)spanEvent.IntrinsicAttributes["name"]);
			Assert.AreEqual(GenericCategory, (string)spanEvent.IntrinsicAttributes["category"]);
			Assert.False(spanEvent.IntrinsicAttributes.ContainsKey("nr.entryPoint"));
		}

		[Test]
		public void GetSpanEvent_ReturnsSpanEventPerSegment_ParentIdIsDistributedTraceGuid_FirstSegmentWithPayload()
		{
			// ARRANGE
			var segments = new List<Segment>()
			{
				_baseGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
			};
			var immutableTransaction = BuildTestTransaction(segments, true, true);

			// ACT
			var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName);
			var spanEvent = spanEvents.ToList()[1];
			var rootSpanEvent = spanEvents.ToList()[0];

			// ASSERT
			Assert.AreEqual((string)rootSpanEvent.IntrinsicAttributes["guid"], (string)spanEvent.IntrinsicAttributes["parentId"]);
		}

		[Test]
		public void GetSpanEvent_ReturnsSpanEventPerSegment_IsRootSegment()
		{
			// ARRANGE
			var segments = new List<Segment>()
			{
				_baseGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
			};
			var immutableTransaction = BuildTestTransaction(segments, true, false);

			// ACT
			var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName);
			var spanEvent = spanEvents.ToList()[0];

			// ASSERT
			Assert.True((bool)spanEvent.IntrinsicAttributes["nr.entryPoint"]);
			Assert.AreEqual(TransactionName, (string)spanEvent.IntrinsicAttributes["name"]);
		}

		#endregion

		#region Datastore

		[Test]
		public void GetSpanEvent_ReturnsSpanEventPerSegment_DatastoreCategory()
		{
			// ARRANGE
			var segments = new List<Segment>()
			{
				_baseDatastoreSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
			};
			var immutableTransaction = BuildTestTransaction(segments, true, false);

			// ACT
			var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName);
			var spanEvent = spanEvents.ToList()[1];

			// ASSERT
			Assert.AreEqual(DatastoreCategory, (string)spanEvent.IntrinsicAttributes["category"]);
		}

		[Test]
		public void GetSpanEvent_ReturnsSpanEventPerSegment_ValidateDatastoreValues()
		{
			// ARRANGE
			var segments = new List<Segment>()
			{
				_baseDatastoreSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>()),
			};
			var immutableTransaction = BuildTestTransaction(segments, true, false);

			// ACT
			var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName);
			var spanEvent = spanEvents.ToList()[1];

			// ASSERT
			Assert.AreEqual(DatastoreVendor.MSSQL.ToString(), (string)spanEvent.IntrinsicAttributes["component"]);
			Assert.AreEqual(ShortObfuscatedQuery, (string)spanEvent.IntrinsicAttributes["db.statement"]);
			Assert.AreEqual(_connectionInfo.DatabaseName, (string)spanEvent.IntrinsicAttributes["db.instance"]);
			Assert.AreEqual($"{_connectionInfo.Host}:{_connectionInfo.PortPathOrId}", (string)spanEvent.IntrinsicAttributes["peer.address"]);
			Assert.AreEqual(_connectionInfo.Host, (string)spanEvent.IntrinsicAttributes["peer.hostname"]);
			Assert.AreEqual("client", (string)spanEvent.IntrinsicAttributes["span.kind"]);
		}

		[TestCase(Query2015Characters1ByteEach, TruncatedQuery2015Characters1ByteEach)]
		[TestCase(Query1015Characters2ByteEach, TruncatedQuery1015Characters2ByteEach)]
		public void GetSpanEvent_ReturnsSpanEventPerSegment_DatastoreTruncateLongStatement(string statement, string expectedStatement)
		{
			// ARRANGE
			var longSqlStatement = new ParsedSqlStatement(DatastoreVendor.MSSQL, statement, "select");
			var longDatastoreSegment = new TypedSegment<DatastoreSegmentData>(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1), new DatastoreSegmentData(longSqlStatement, statement, _connectionInfo), false);

			var segments = new List<Segment>()
			{
				longDatastoreSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>()),
			};
			var immutableTransaction = BuildTestTransaction(segments, true, false);

			// ACT
			var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName);
			var spanEvent = spanEvents.ToList()[1];

			// ASSERT
			var actualStatement = (string) spanEvent.IntrinsicAttributes["db.statement"];
			var statementBytes = Encoding.UTF8.GetByteCount(actualStatement);
			Assert.AreEqual(expectedStatement, actualStatement);
			Assert.True(statementBytes <= 2000);
			Assert.AreEqual(2000, statementBytes, 3);
		}

		#endregion

		#region Http (Externals)

		[Test]
		public void GetSpanEvent_ReturnsSpanEventPerSegment_HttpCategory()
		{
			// ARRANGE
			var segments = new List<Segment>()
			{
				_baseHttpSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
			};
			var immutableTransaction = BuildTestTransaction(segments, true, false);

			// ACT
			var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName);
			var spanEvent = spanEvents.ToList()[1];

			// ASSERT
			Assert.AreEqual(HttpCategory, (string)spanEvent.IntrinsicAttributes["category"]);
		}

		[Test]
		public void GetSpanEvent_ReturnsSpanEventPerSegment_ValidateHttpValues()
		{
			// ARRANGE
			var segments = new List<Segment>()
			{
				_baseHttpSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>()),
			};
			var immutableTransaction = BuildTestTransaction(segments, true, false);

			// ACT
			var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName);
			var spanEvent = spanEvents.ToList()[1];

			// ASSERT
			Assert.AreEqual(HttpUri, (string)spanEvent.IntrinsicAttributes["http.url"]);
			Assert.AreEqual(HttpMethod, (string)spanEvent.IntrinsicAttributes["http.method"]);
			Assert.AreEqual("type", (string)spanEvent.IntrinsicAttributes["component"]);
			Assert.AreEqual("client", (string)spanEvent.IntrinsicAttributes["span.kind"]);
		}

		#endregion

		private ImmutableTransaction BuildTestTransaction(List<Segment> segments, bool sampled, bool hasIncomingPayload)
		{
			return new ImmutableTransactionBuilder()
				.IsWebTransaction("foo", "bar")
				.WithUserErrorAttribute("CustomErrorAttrKey", "CustomErrorAttrValue")
				.WithPriority(Priority)
				.WithDistributedTracing(DistributedTraceGuid, DistributedTraceTraceId, sampled, hasIncomingPayload)
				.WithSegments(segments)
				.WithStartTime(_startTime)
				.WithTransactionGuid(_transactionGuid)
				.Build();
		}

		public static ITransactionSegmentState CreateTransactionSegmentState(int uniqueId, int? parentId, int managedThreadId = 1)
		{
			var segmentState = Mock.Create<ITransactionSegmentState>();
			Mock.Arrange(() => segmentState.ParentSegmentId()).Returns(parentId);
			Mock.Arrange(() => segmentState.CallStackPush(Arg.IsAny<Segment>())).Returns(uniqueId);
			Mock.Arrange(() => segmentState.CurrentManagedThreadId).Returns(managedThreadId);
			return segmentState;
		}
	}
}