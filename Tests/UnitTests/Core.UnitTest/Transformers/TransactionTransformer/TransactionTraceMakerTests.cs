using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Extensions.Parsing;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	[TestFixture]
	public class TransactionTraceMakerTests
	{
		[NotNull]
		private TransactionTraceMaker _transactionTraceMaker;

		[NotNull]
		private IAttributeService _attributeService;

		[NotNull]
		private IDatabaseService _databaseService;

		[NotNull]
		private IConfigurationService _configurationService;

		[SetUp]
		public void SetUp()
		{
			_databaseService = Mock.Create<IDatabaseService>();
			Mock.Arrange(() => _databaseService.SqlObfuscator.GetObfuscatedSql(Arg.IsAny<String>())).Returns((String sql) => "Obfuscated " + sql);

			_configurationService = Mock.Create<IConfigurationService>();
			Mock.Arrange(() => _configurationService.Configuration.DatabaseNameReportingEnabled).Returns(true);
			Mock.Arrange(() => _configurationService.Configuration.InstanceReportingEnabled).Returns(true);

			_attributeService = Mock.Create<IAttributeService>();
			Mock.Arrange(() => _attributeService.FilterAttributes(Arg.IsAny<Attributes>(), AttributeDestinations.TransactionTrace))
				.Returns<Attributes, AttributeDestinations>((attributes, _) => attributes);

			_transactionTraceMaker = new TransactionTraceMaker(_attributeService, _configurationService);
		}

		[Test]
		public void GetTransactionTrace_CreatesTraceWithSql()
		{
			var expectedParameter = "SELECT * FROM test_table WHERE foo = 1";
			var transaction = BuildTestTransaction(startTime: DateTime.Now.AddSeconds(-50));
			var segments = new[] { BuildDataStoreSegmentNode()};
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TrxName");
			var attributes = new Attributes();

			var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);

			Assert.IsTrue(trace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters.ContainsKey("sql"));

			var actualParameter = trace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters["sql"];

			Assert.IsNotNull(actualParameter);
			Assert.IsNotEmpty(actualParameter as string);
			Assert.AreNotEqual(expectedParameter, actualParameter);
		}

		[Test]
		public void GetTransactionTrace_CreatesTraceWithDatastoreInstanceInformation()
		{
			var expectedDatabaseParameter = "My Database";
			var expectedPortParameter = "My Port";
			var expectedHostParameter = "My Host";

			var transaction = BuildTestTransaction();
			var segments = new[] {BuildDataStoreSegmentNodeWithInstanceData()};
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TrxName");
			var attributes = new Attributes();

			var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);

			Assert.IsTrue(trace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters.ContainsKey("database_name"));
			Assert.IsTrue(trace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters.ContainsKey("host"));
			Assert.IsTrue(trace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters.ContainsKey("port_path_or_id")); 

			var actualDatabaseParameter = trace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters["database_name"];
			var actualHostParameter = trace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters["host"];
			var actualPathPortParameter = trace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters["port_path_or_id"];

			Assert.AreEqual(expectedDatabaseParameter, actualDatabaseParameter);
			Assert.AreEqual(expectedHostParameter, actualHostParameter);
			Assert.AreEqual(expectedPortParameter, actualPathPortParameter);
		}

		[Test]
		public void GetTransactionTrace_CreatesTraceWithCorrectStartTime()
		{
			var expectedStartTime = DateTime.Now.AddSeconds(-50);
			var transaction = BuildTestTransaction(startTime: expectedStartTime);
			var segments = new[] {BuildNode()};
			var transactionMetricName = new TransactionMetricName("WebTranasction", "TrxName");
			var attributes = new Attributes();

			var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);

			Assert.AreEqual(expectedStartTime, trace.StartTime);
		}

		[Test]
		public void GetTransactionTrace_CreatesTraceWithCorrectDuration()
		{
			var expectedDuration = TimeSpan.FromSeconds(5);
			var transaction = BuildTestTransaction(duration: expectedDuration);
			var segments = new[] {BuildNode()};
			var transactionMetricName = new TransactionMetricName("WebTranasction", "TrxName");
			var attributes = new Attributes();

			var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);

			Assert.AreEqual(expectedDuration, trace.Duration);
		}

		[Test]
		public void GetTransactionTrace_CreatesTraceWithCorrectUri()
		{
			const String inputUrl = "http://www.google.com/test?param=value";
			var transaction = BuildTestTransaction(uri: inputUrl);
			var segments = new[] {BuildNode()};
			var transactionMetricName = new TransactionMetricName("WebTranasction", "TrxName");
			var attributes = new Attributes();

			var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);

			// Query parameters should be stripped out
			const String expectedUri = "http://www.google.com/test";
			Assert.AreEqual(expectedUri, trace.Uri);
		}

		[Test]
		public void GetTransactionTrace_CreatesTraceWithCorrectGuid()
		{
			var expectedGuid = Guid.NewGuid().ToString();
			var transaction = BuildTestTransaction(guid: expectedGuid);
			var segments = new[] {BuildNode()};
			var transactionMetricName = new TransactionMetricName("WebTranasction", "TrxName");
			var attributes = new Attributes();

			var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);

			Assert.AreEqual(expectedGuid, trace.Guid);
		}

		[Test]
		public void GetTransactionTrace_Throws_IfSegmentTreeIsEmpty()
		{
			var transaction = BuildTestTransaction();
			var segments = Enumerable.Empty<ImmutableSegmentTreeNode>();
			var transactionMetricName = new TransactionMetricName("WebTranasction", "TrxName");
			var attributes = new Attributes();

			Assert.Throws<ArgumentException>(() => _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes));
		}

		[Test]
		public void GetTransactionTrace_PrependsTreeWithRootNodeAndFauxTopLevelSegment()
		{
			var expectedStartTimeDifference = TimeSpan.FromSeconds(0);
			var expectedEndTimeDifference = TimeSpan.FromSeconds(10);
			var transaction = BuildTestTransaction(duration: expectedEndTimeDifference);
			var segments = new[] {BuildNode()};
			var transactionMetricName = new TransactionMetricName("WebTranasction", "TrxName");
			var attributes = new Attributes();

			var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);
			var root = trace.TransactionTraceData.RootSegment;
			var fauxTopLevelSegment = root.Children.First();

			NrAssert.Multiple(
				// ROOT
				() => Assert.AreEqual(expectedStartTimeDifference, root.TimeBetweenTransactionStartAndSegmentStart),
				() => Assert.AreEqual(expectedEndTimeDifference, root.TimeBetweenTransactionStartAndSegmentEnd),
				() => Assert.AreEqual("ROOT", root.Name),
				() => Assert.AreEqual(1, root.Children.Count),

				// Faux top-level segment
				() => Assert.AreEqual(expectedStartTimeDifference, fauxTopLevelSegment.TimeBetweenTransactionStartAndSegmentStart),
				() => Assert.AreEqual(expectedEndTimeDifference, fauxTopLevelSegment.TimeBetweenTransactionStartAndSegmentEnd),
				() => Assert.AreEqual("Transaction", fauxTopLevelSegment.Name),
				() => Assert.AreEqual(1, fauxTopLevelSegment.Children.Count)
				);
		}

		[Test]
		public void GetTransactionTrace_AppendsNodeToFauxTopLevelSegmentChildren()
		{
			var transactionStartTime = DateTime.Now;
			var segmentStartTime = transactionStartTime.AddSeconds(1);
			var expectedStartTimeDifference = TimeSpan.FromSeconds(1);
			var segmentDuration = TimeSpan.FromSeconds(10);
			var expectedEndTimeDifference = expectedStartTimeDifference + segmentDuration;
			const String expectedName = "some segment name";
			var expectedParameters = new Dictionary<String, Object> {{"foo", "bar"}};
			var expectedClassName = "foo";
			var expectedMethodName = "bar";
			var methodCallData = new MethodCallData(expectedClassName, expectedMethodName, 1);
			var transaction = BuildTestTransaction(startTime: transactionStartTime);
			var segments = new[] {BuildNode(transaction, startTime: segmentStartTime, duration: segmentDuration, name: expectedName, parameters: expectedParameters, methodCallData: methodCallData) };
			var transactionMetricName = new TransactionMetricName("WebTranasction", "TrxName");
			var attributes = new Attributes();

			var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);
			var realSegments = trace.TransactionTraceData.RootSegment.Children.First().Children;
			var firstSegment = realSegments.First();

			NrAssert.Multiple(
				() => Assert.AreEqual(expectedStartTimeDifference, firstSegment.TimeBetweenTransactionStartAndSegmentStart),
				() => Assert.AreEqual(expectedEndTimeDifference, firstSegment.TimeBetweenTransactionStartAndSegmentEnd),
				() => Assert.AreEqual(expectedName, firstSegment.Name),
				() => Assert.AreEqual(expectedClassName, firstSegment.ClassName),
				() => Assert.AreEqual(expectedMethodName, firstSegment.MethodName),
				() => Assert.AreEqual(0, firstSegment.Children.Count),
				() => Assert.True(expectedParameters.All(kvp => expectedParameters[kvp.Key] == firstSegment.Parameters[kvp.Key]))
				);
		}

		[Test]
		public void GetTransactionTrace_AddsAsyncParametersToAllNodes()
		{
			var now = new TimeSpan();
			var node1 = GetNodeBuilder(name: "1", startTime: now, duration: TimeSpan.FromSeconds(1));
			var node2 = GetNodeBuilder(name: "2", startTime: now, duration: TimeSpan.FromSeconds(.5));
			var node11 = GetNodeBuilder(name: "1.1", startTime: now, duration: TimeSpan.FromSeconds(.25));
			node1.Children.Add(node11);
			node1.Segment.ChildFinished(node11.Segment);

			var transaction = BuildTestTransaction();
			var topLevelSegments = new[] {node1.Build(), node2.Build()};
			var transactionMetricName = new TransactionMetricName("WebTranasction", "TrxName");
			var attributes = new Attributes();

			var trace = _transactionTraceMaker.GetTransactionTrace(transaction, topLevelSegments, transactionMetricName, attributes);

			var realSegments = trace.TransactionTraceData.RootSegment.Children.First().Children;
			var segment1 = realSegments.ElementAt(0);
			var segment2 = realSegments.ElementAt(1);
			var segment11 = segment1.Children.ElementAt(0);

			NrAssert.Multiple(
				() => Assert.AreEqual(750, segment1.Parameters["exclusive_duration_millis"]),
				() => Assert.AreEqual(500, segment2.Parameters["exclusive_duration_millis"]),
				() => Assert.AreEqual(250, segment11.Parameters["exclusive_duration_millis"])
				);
		}

		[Test]
		public void GetTransactionTrace_RetainsComplicatedSegmentTreeStructure()
		{
			var node1 = GetNodeBuilder(name: "1");
			var node2 = GetNodeBuilder(name: "2");
			var node11 = GetNodeBuilder(name: "1.1");
			var node12 = GetNodeBuilder(name: "1.2");
			var node121 = GetNodeBuilder(name: "1.2.1");

			node1.Children.Add(node11);
			node1.Children.Add(node12);
			node12.Children.Add(node121);

			var transaction = BuildTestTransaction();
			var topLevelSegments = new[] {node1.Build(), node2.Build()};
			var transactionMetricName = new TransactionMetricName("WebTranasction", "TrxName");
			var attributes = new Attributes();

			var trace = _transactionTraceMaker.GetTransactionTrace(transaction, topLevelSegments, transactionMetricName, attributes);

			var realSegments = trace.TransactionTraceData.RootSegment.Children.First().Children;
			var segment1 = realSegments.ElementAt(0);
			var segment2 = realSegments.ElementAt(1);
			var segment11 = segment1.Children.ElementAt(0);
			var segment12 = segment1.Children.ElementAt(1);
			var segment121 = segment12.Children.ElementAt(0);

			NrAssert.Multiple(
				() => Assert.AreEqual("1", segment1.Name),
				() => Assert.AreEqual("2", segment2.Name),
				() => Assert.AreEqual("1.1", segment11.Name),
				() => Assert.AreEqual("1.2", segment12.Name),
				() => Assert.AreEqual("1.2.1", segment121.Name)
				);
		}

		[NotNull]
		private static ImmutableSegmentTreeNode BuildNode(ImmutableTransaction transaction = null, DateTime? startTime = null, TimeSpan? duration = null, String name = "", MethodCallData methodCallData = null, IEnumerable<KeyValuePair<String, Object>> parameters = null)
		{
			startTime = startTime ?? DateTime.Now;
			var relativeStart = startTime.Value - (transaction?.StartTime ?? startTime.Value);
			methodCallData = methodCallData ?? new MethodCallData("typeName", "methodName", 1);
			return new SegmentTreeNodeBuilder(SimpleSegmentDataTests.createSimpleSegmentBuilder(relativeStart, duration ?? TimeSpan.Zero, 2, 1, methodCallData, parameters ?? new Dictionary<String, Object>(), name, false))
				.Build();
		}

		private static ImmutableSegmentTreeNode BuildDataStoreSegmentNode(TimeSpan startTime = new TimeSpan(), TimeSpan? duration = null, String name = "", MethodCallData methodCallData = null, IEnumerable<KeyValuePair<String, Object>> parameters = null)
		{
			methodCallData = methodCallData ?? new MethodCallData("typeName", "methodName", 1);

			var data = new DatastoreSegmentData(new ParsedSqlStatement(DatastoreVendor.MSSQL, "test_table", "SELECT"), "SELECT * FROM test_table");

			return new SegmentTreeNodeBuilder(
				new TypedSegment<DatastoreSegmentData>(startTime, duration ?? TimeSpan.Zero, new TypedSegment<DatastoreSegmentData>(Mock.Create<ITransactionSegmentState>(), methodCallData, data, false)))
				.Build();
		}

		private static ImmutableSegmentTreeNode BuildDataStoreSegmentNodeWithInstanceData(TimeSpan startTime = new TimeSpan(), TimeSpan? duration = null, String name = "", MethodCallData methodCallData = null, IEnumerable<KeyValuePair<String, Object>> parameters = null)
		{
			methodCallData = methodCallData ?? new MethodCallData("typeName", "methodName", 1);

			var data = new DatastoreSegmentData(new ParsedSqlStatement(DatastoreVendor.MSSQL, "test_table", "SELECT"),
				"SELECT * FROM test_table",
				new ConnectionInfo("My Host", "My Port", "My Database"));

			return new SegmentTreeNodeBuilder(new TypedSegment<DatastoreSegmentData>(startTime, duration ?? TimeSpan.Zero, 
				new TypedSegment<DatastoreSegmentData>(Mock.Create<ITransactionSegmentState>(), methodCallData, data, false)))
				.Build();
		}

		[NotNull]
		private static SegmentTreeNodeBuilder GetNodeBuilder(TimeSpan startTime = new TimeSpan(), TimeSpan? duration = null, String name = "", MethodCallData methodCallData = null, IEnumerable<KeyValuePair<String, Object>> parameters = null)
		{
			methodCallData = methodCallData ?? new MethodCallData("typeName", "methodName", 1);
			return new SegmentTreeNodeBuilder(SimpleSegmentDataTests.createSimpleSegmentBuilder(startTime, duration ?? TimeSpan.Zero, 2, 1, methodCallData, parameters ?? new Dictionary<String, Object>(), name, false));
		}

		private ImmutableTransaction BuildTestTransaction(DateTime? startTime = null, TimeSpan? duration = null, String uri = null, String guid = null)
		{
			var transactionMetadata = new TransactionMetadata();
			if (uri != null)
				transactionMetadata.SetUri(uri);

			var name = new WebTransactionName("foo", "bar");
			var segments = Enumerable.Empty<Segment>();
			var metadata = transactionMetadata.ConvertToImmutableMetadata();
			startTime = startTime ?? DateTime.Now;
			duration = duration ?? TimeSpan.FromSeconds(1);
			guid = guid ?? Guid.NewGuid().ToString();

			return new ImmutableTransaction(name, segments, metadata, startTime.Value, duration.Value, guid, false, false, false, _databaseService.SqlObfuscator);
		}
	}
}
