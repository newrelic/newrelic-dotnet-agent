using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NewRelic.Core;
using NewRelic.Parsing;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Spans.Tests
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
		private const string W3cParentId = "w3cParentId";
		private const string Vendor1 = "dd";
		private const string Vendor2 = "earl";
		private readonly List<string> VendorStateEntries = new List<string>() { $"{Vendor1}=YzRiMTIxODk1NmVmZTE4ZQ", $"{Vendor2}=aaaaaaaaaaaaaa" };

		private const string GenericCategory = "generic";
		private const string DatastoreCategory = "datastore";
		private const string HttpCategory = "http";
		private const string ShortQuery = "Select * from users where ssn = 433871122";

		private const string HttpUri = "http://localhost:80/api/test";
		private const string HttpMethod = "GET";

		private const string TransactionName = "WebTransaction/foo/bar";

		private SpanEventMaker _spanEventMaker;
		private IDatabaseService _databaseService;
		private string _transactionGuid;
		private DateTime _startTime;
		private Segment _baseGenericSegment;
		private Segment _childGenericSegment;
		private Segment _baseDatastoreSegment;
		private Segment _baseHttpSegment;

		private string _obfuscatedSql;
		private ParsedSqlStatement _parsedSqlStatement;
		private ConnectionInfo _connectionInfo;

		[SetUp]
		public void SetUp()
		{
			var attributeService = Mock.Create<IAttributeService>();
			Mock.Arrange(() => attributeService.FilterAttributes(Arg.IsAny<AttributeCollection>(), Arg.IsAny<AttributeDestinations>())).Returns<AttributeCollection, AttributeDestinations>((attrs, _) => attrs);

			var attribDefSvc = new AttributeDefinitionService((f)=>new AttributeDefinitions(f));

			_spanEventMaker = new SpanEventMaker(attribDefSvc);
			_databaseService = new DatabaseService(Mock.Create<ICacheStatsReporter>());

			_transactionGuid = GuidGenerator.GenerateNewRelicGuid();
			_startTime = new DateTime(2018, 7, 18, 7, 0, 0, DateTimeKind.Utc); // unixtime = 1531897200000

			// Generic Segments
			_baseGenericSegment = new Segment(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1), new SpanAttributeValueCollection());
			_baseGenericSegment.SetSegmentData(new SimpleSegmentData(SegmentName));
			
			_childGenericSegment = new Segment(CreateTransactionSegmentState(4, 3, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1), new SpanAttributeValueCollection());
			_childGenericSegment.SetSegmentData(new SimpleSegmentData(SegmentName));

			// Datastore Segments
			_connectionInfo = new ConnectionInfo("localhost", "1234", "default", "maininstance");
			_parsedSqlStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, System.Data.CommandType.Text, ShortQuery);
			_obfuscatedSql = _databaseService.GetObfuscatedSql(ShortQuery, DatastoreVendor.MSSQL);
			_baseDatastoreSegment = new Segment(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1), new SpanAttributeValueCollection());
			_baseDatastoreSegment.SetSegmentData(new DatastoreSegmentData(_databaseService, _parsedSqlStatement, ShortQuery, _connectionInfo));

			// Http Segments
			_baseHttpSegment = new Segment(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1), new SpanAttributeValueCollection());
			_baseHttpSegment.SetSegmentData(new ExternalSegmentData(new Uri(HttpUri), HttpMethod));
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
			var spanEventIntrinsicAttributes = spanEvent.IntrinsicAttributes();

			// ASSERT
			Assert.AreEqual("Span", (string)spanEventIntrinsicAttributes["type"]);
			Assert.AreEqual(DistributedTraceTraceId, (string)spanEventIntrinsicAttributes["traceId"]);
			Assert.AreEqual(_childGenericSegment.SpanId, (string)spanEventIntrinsicAttributes["guid"]);
			Assert.AreEqual(_baseGenericSegment.SpanId, (string)spanEventIntrinsicAttributes["parentId"]);
			Assert.AreEqual(_transactionGuid, (string)spanEventIntrinsicAttributes["transactionId"]);
			Assert.AreEqual(true, (bool)spanEventIntrinsicAttributes["sampled"]);
			Assert.AreEqual(Priority, (double)spanEventIntrinsicAttributes["priority"]);
			Assert.AreEqual(1531897200001, (long)spanEventIntrinsicAttributes["timestamp"]);
			Assert.AreEqual(0.005, (double)spanEventIntrinsicAttributes["duration"]);
			Assert.AreEqual(SegmentName, (string)spanEventIntrinsicAttributes["name"]);
			Assert.AreEqual(GenericCategory, (string)spanEventIntrinsicAttributes["category"]);
			Assert.False(spanEventIntrinsicAttributes.ContainsKey("nr.entryPoint"));
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
			Assert.AreEqual((string)rootSpanEvent.IntrinsicAttributes()["guid"], (string)spanEvent.IntrinsicAttributes()["parentId"]);
		}

		[Test]
		public void GetSpanEvent_ReturnsSpanEventPerSegment_W3CAttributes()
		{
			// ARRANGE
			var segments = new List<Segment>()
			{
				_baseGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
			};

			var immutableTransaction = new ImmutableTransactionBuilder()
				.WithW3CTracing(DistributedTraceGuid, W3cParentId, VendorStateEntries)
				.Build();

			// ACT
			var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName);
			var spanEvent = spanEvents.ToList()[1];
			var rootSpanEvent = spanEvents.ToList()[0];

			// ASSERT
			Assert.AreEqual(W3cParentId, (string)rootSpanEvent.IntrinsicAttributes()["parentId"]);
			Assert.AreEqual(DistributedTraceGuid, (string)rootSpanEvent.IntrinsicAttributes()["trustedParentId"]);
			Assert.AreEqual($"{Vendor1},{Vendor2}", (string)rootSpanEvent.IntrinsicAttributes()["tracingVendors"]);
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
			Assert.True((bool)spanEvent.IntrinsicAttributes()["nr.entryPoint"]);
			Assert.AreEqual(TransactionName, (string)spanEvent.IntrinsicAttributes()["name"]);
		}

		#endregion

		#region Datastore

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
			var spanEventIntrinsicAttributes = spanEvent.IntrinsicAttributes();
			var spanEventAgentAttributes = spanEvent.AgentAttributes();

			// ASSERT
			Assert.AreEqual(DatastoreCategory, (string)spanEventIntrinsicAttributes["category"]);
			Assert.AreEqual(DatastoreVendor.MSSQL.ToString(), (string)spanEventIntrinsicAttributes["component"]);
			Assert.AreEqual(_parsedSqlStatement.Model, (string)spanEventAgentAttributes["db.collection"]);
			Assert.AreEqual(_obfuscatedSql, (string)spanEventAgentAttributes["db.statement"]);
			Assert.AreEqual(_connectionInfo.DatabaseName, (string)spanEventAgentAttributes["db.instance"]);
			Assert.AreEqual($"{_connectionInfo.Host}:{_connectionInfo.PortPathOrId}", (string)spanEventAgentAttributes["peer.address"]);
			Assert.AreEqual(_connectionInfo.Host, (string)spanEventAgentAttributes["peer.hostname"]);
			Assert.AreEqual("client", (string)spanEventIntrinsicAttributes["span.kind"]);
		}

		public void GetSpanEvent_ReturnsSpanEventPerSegment_DatastoreTruncateLongStatement()
		{

			var customerStmt = new string[] 
			{
				new string('U', 2015),				//1-byte per char
				new string('仮', 1015)				//3-bytes per char
			};

			var expectedStmtTrunc = new string[]
			{
				new string('U', 1996) + "...",		//1-byte per char
				new string('仮', 666) + "..."		//3-bytes per char
			};

			for(int i = 0; i < customerStmt.Length; i++)
			{
				// ARRANGE
				var longSqlStatement = new ParsedSqlStatement(DatastoreVendor.MSSQL, customerStmt[i], "select");
				var longDatastoreSegment = new Segment(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1), new SpanAttributeValueCollection());
				longDatastoreSegment.SetSegmentData(new DatastoreSegmentData(_databaseService, longSqlStatement, customerStmt[i], _connectionInfo));

				var segments = new List<Segment>()
								{
									longDatastoreSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>()),
								};
				var immutableTransaction = BuildTestTransaction(segments, true, false);

				// ACT
				var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName);
				var spanEvent = spanEvents.ToList()[1];

				// ASSERT
				var attribStatement = (string)spanEvent.AgentAttributes()["db.statement"];
				var attribStmtLenBytes = Encoding.UTF8.GetByteCount(attribStatement);

				Assert.AreEqual(expectedStmtTrunc[i], attribStatement);
				Assert.True(attribStmtLenBytes <= 1999);
				Assert.True(attribStmtLenBytes >= 1996);
			}
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
			Assert.AreEqual(HttpCategory, (string)spanEvent.IntrinsicAttributes()["category"]);
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
			var spanEventIntrinsicAttributes = spanEvent.IntrinsicAttributes();
			var spanEventAgentAttributes = spanEvent.AgentAttributes();

			// ASSERT
			Assert.AreEqual(HttpUri, (string)spanEventAgentAttributes["http.url"]);
			Assert.AreEqual(HttpMethod, (string)spanEventAgentAttributes["http.method"]);
			Assert.AreEqual("type", (string)spanEventIntrinsicAttributes["component"]);
			Assert.AreEqual("client", (string)spanEventIntrinsicAttributes["span.kind"]);
		}

		[Test]
		public void GetSpanEvent_ReturnsSpanEventPerSegment_NoHttpStatusCode()
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
			CollectionAssert.DoesNotContain(spanEvent.AgentAttributes().Keys, "http.statusCode");
		}

		[Test]
		public void GetSpanEvent_ReturnsSpanEventPerSegment_HasHttpStatusCode()
		{
			// ARRANGE
			var segments = new List<Segment>()
			{
				_baseHttpSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
			};
			var externalSegmentData = segments[0].Data as ExternalSegmentData;
			externalSegmentData.SetHttpStatusCode(200);

			var immutableTransaction = BuildTestTransaction(segments, true, false);

			// ACT
			var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName);
			var spanEvent = spanEvents.ToList()[1];

			// ASSERT
			Assert.AreEqual(200, spanEvent.AgentAttributes()["http.statusCode"]);
		}

		#endregion

		private ImmutableTransaction BuildTestTransaction(List<Segment> segments, bool sampled, bool hasIncomingPayload)
		{
			return new ImmutableTransactionBuilder()
				.IsWebTransaction("foo", "bar")
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
			Mock.Arrange(() => segmentState.AttribDefs).Returns(() => new AttributeDefinitions(new AttributeFilter(new AttributeFilter.Settings())));
			Mock.Arrange(() => segmentState.ParentSegmentId()).Returns(parentId);
			Mock.Arrange(() => segmentState.CallStackPush(Arg.IsAny<Segment>())).Returns(uniqueId);
			Mock.Arrange(() => segmentState.CurrentManagedThreadId).Returns(managedThreadId);
			return segmentState;
		}
	}
}
