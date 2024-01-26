// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Spans;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer.UnitTest
{
    [TestFixture]
    public class TransactionTraceMakerTests
    {
        private TransactionTraceMaker _transactionTraceMaker;

        private IDatabaseService _databaseService;

        private IConfigurationService _configurationService;

        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;

        [SetUp]
        public void SetUp()
        {
            _databaseService = Mock.Create<IDatabaseService>();
            Mock.Arrange(() => _databaseService.GetObfuscatedSql(Arg.IsAny<string>(), Arg.IsAny<DatastoreVendor>())).Returns((string sql) => "Obfuscated " + sql);

            _configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => _configurationService.Configuration.DatabaseNameReportingEnabled).Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.InstanceReportingEnabled).Returns(true);

            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));

            _transactionTraceMaker = new TransactionTraceMaker(_configurationService, _attribDefSvc);

        }

        [TearDown]
        public void TearDown()
        {
            _attribDefSvc.Dispose();
            _databaseService.Dispose();
        }

        [Test]
        public void GetTransactionTrace_CreatesTraceWithSql()
        {
            var expectedParameter = "SELECT * FROM test_table WHERE foo = 1";
            var transaction = BuildTestTransaction(startTime: DateTime.Now.AddSeconds(-50));
            var segments = new[] { BuildDataStoreSegmentNode() };
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TrxName");
            var attributes = new AttributeValueCollection(AttributeDestinations.TransactionTrace);

            var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);

            Assert.That(trace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters.ContainsKey("sql"), Is.True);

            var actualParameter = trace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters["sql"];

            Assert.That(actualParameter, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(actualParameter as string, Is.Not.Empty);
                Assert.That(actualParameter, Is.Not.EqualTo(expectedParameter));
            });
        }

        [Test]
        public void GetTransactionTrace_CreatesTraceWithDatastoreInstanceInformation()
        {
            var expectedDatabaseParameter = "My Database";
            var expectedPortParameter = "My Port";
            var expectedHostParameter = "My Host";

            var transaction = BuildTestTransaction();
            var segments = new[] { BuildDataStoreSegmentNodeWithInstanceData() };
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TrxName");
            var attributes = new AttributeValueCollection(AttributeDestinations.TransactionTrace);

            var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);

            Assert.Multiple(() =>
            {
                Assert.That(trace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters.ContainsKey("database_name"), Is.True);
                Assert.That(trace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters.ContainsKey("host"), Is.True);
                Assert.That(trace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters.ContainsKey("port_path_or_id"), Is.True);
            });

            var actualDatabaseParameter = trace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters["database_name"];
            var actualHostParameter = trace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters["host"];
            var actualPathPortParameter = trace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters["port_path_or_id"];

            Assert.Multiple(() =>
            {
                Assert.That(actualDatabaseParameter, Is.EqualTo(expectedDatabaseParameter));
                Assert.That(actualHostParameter, Is.EqualTo(expectedHostParameter));
                Assert.That(actualPathPortParameter, Is.EqualTo(expectedPortParameter));
            });
        }

        [Test]
        public void GetTransactionTrace_CreatesTraceWithCorrectStartTime()
        {
            var expectedStartTime = DateTime.Now.AddSeconds(-50);
            var transaction = BuildTestTransaction(startTime: expectedStartTime);
            var segments = new[] { BuildNode() };
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TrxName");
            var attributes = new AttributeValueCollection(AttributeDestinations.TransactionTrace);

            var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);

            Assert.That(trace.StartTime, Is.EqualTo(expectedStartTime));
        }

        [Test]
        public void GetTransactionTrace_CreatesTraceWithCorrectDuration()
        {
            var expectedDuration = TimeSpan.FromSeconds(5);
            var transaction = BuildTestTransaction(duration: expectedDuration);
            var segments = new[] { BuildNode() };
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TrxName");
            var attributes = new AttributeValueCollection(AttributeDestinations.TransactionTrace);

            var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);

            Assert.That(trace.Duration, Is.EqualTo(expectedDuration));
        }

        [Test]
        public void GetTransactionTrace_CreatesTraceWithCorrectResponseTime()
        {
            var expectedDuration = TimeSpan.FromSeconds(7);
            var expectedResponseTime = TimeSpan.FromSeconds(3);
            var transaction = BuildTestTransaction(duration: expectedDuration, responseTime: expectedResponseTime);
            var segments = new[] { BuildNode() };
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TrxName");
            var attributes = new AttributeValueCollection(AttributeDestinations.TransactionTrace);

            var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);

            Assert.That(trace.Duration, Is.EqualTo(expectedResponseTime));
        }

        [Test]
        public void GetTransactionTrace_CreatesTraceWithCorrectUri()
        {
            //Mock.Arrange(() => _attributeService.AllowRequestUri(AttributeDestinations.TransactionTrace))
            //    .Returns(true);

            const string inputUrl = "http://www.google.com/test?param=value";
            var transaction = BuildTestTransaction(uri: inputUrl);
            var segments = new[] { BuildNode() };
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TrxName");
            var attributes = new AttributeValueCollection(AttributeDestinations.TransactionTrace);

            var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);

            // Query parameters should be stripped out
            const string expectedUri = "http://www.google.com/test";
            Assert.That(trace.Uri, Is.EqualTo(expectedUri));
        }

        [Test]
        public void GetTransactionTrace_CreatesTraceWithCorrectGuid()
        {
            var expectedGuid = Guid.NewGuid().ToString();
            var transaction = BuildTestTransaction(guid: expectedGuid);
            var segments = new[] { BuildNode() };
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TrxName");
            var attributes = new AttributeValueCollection(AttributeDestinations.TransactionTrace);

            var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);

            Assert.That(trace.Guid, Is.EqualTo(expectedGuid));
        }

        [Test]
        public void GetTransactionTrace_Throws_IfSegmentTreeIsEmpty()
        {
            var transaction = BuildTestTransaction();
            var segments = Enumerable.Empty<ImmutableSegmentTreeNode>();
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TrxName");
            var attributes = new AttributeValueCollection(AttributeDestinations.TransactionTrace);

            Assert.Throws<ArgumentException>(() => _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes));
        }

        [Test]
        public void GetTransactionTrace_PrependsTreeWithRootNodeAndFauxTopLevelSegment()
        {
            var expectedStartTimeDifference = TimeSpan.FromSeconds(0);
            var expectedEndTimeDifference = TimeSpan.FromSeconds(10);
            var transaction = BuildTestTransaction(duration: expectedEndTimeDifference);
            var segments = new[] { BuildNode() };
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TrxName");
            var attributes = new AttributeValueCollection(AttributeDestinations.TransactionTrace);

            var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);
            var root = trace.TransactionTraceData.RootSegment;
            var fauxTopLevelSegment = root.Children.First();

            NrAssert.Multiple(
                // ROOT
                () => Assert.That(root.TimeBetweenTransactionStartAndSegmentStart, Is.EqualTo(expectedStartTimeDifference)),
                () => Assert.That(root.TimeBetweenTransactionStartAndSegmentEnd, Is.EqualTo(expectedEndTimeDifference)),
                () => Assert.That(root.Name, Is.EqualTo("ROOT")),
                () => Assert.That(root.Children, Has.Count.EqualTo(1)),

                // Faux top-level segment
                () => Assert.That(fauxTopLevelSegment.TimeBetweenTransactionStartAndSegmentStart, Is.EqualTo(expectedStartTimeDifference)),
                () => Assert.That(fauxTopLevelSegment.TimeBetweenTransactionStartAndSegmentEnd, Is.EqualTo(expectedEndTimeDifference)),
                () => Assert.That(fauxTopLevelSegment.Name, Is.EqualTo("Transaction")),
                () => Assert.That(fauxTopLevelSegment.Children, Has.Count.EqualTo(1))
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
            const string expectedName = "some segment name";
            var expectedParameters = new Dictionary<string, object> { { "foo", "bar" } };
            var expectedClassName = "foo";
            var expectedMethodName = "bar";
            var methodCallData = new MethodCallData(expectedClassName, expectedMethodName, 1);
            var transaction = BuildTestTransaction(startTime: transactionStartTime);
            var segments = new[] { BuildNode(transaction, startTime: segmentStartTime, duration: segmentDuration, name: expectedName, parameters: expectedParameters, methodCallData: methodCallData) };
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TrxName");
            var attributes = new AttributeValueCollection(AttributeDestinations.TransactionTrace);

            var trace = _transactionTraceMaker.GetTransactionTrace(transaction, segments, transactionMetricName, attributes);
            var realSegments = trace.TransactionTraceData.RootSegment.Children.First().Children;
            var firstSegment = realSegments.First();

            NrAssert.Multiple(
                () => Assert.That(firstSegment.TimeBetweenTransactionStartAndSegmentStart, Is.EqualTo(expectedStartTimeDifference)),
                () => Assert.That(firstSegment.TimeBetweenTransactionStartAndSegmentEnd, Is.EqualTo(expectedEndTimeDifference)),
                () => Assert.That(firstSegment.Name, Is.EqualTo(expectedName)),
                () => Assert.That(firstSegment.ClassName, Is.EqualTo(expectedClassName)),
                () => Assert.That(firstSegment.MethodName, Is.EqualTo(expectedMethodName)),
                () => Assert.That(firstSegment.Children, Is.Empty),
                () => Assert.That(expectedParameters.All(kvp => expectedParameters[kvp.Key] == firstSegment.Parameters[kvp.Key]), Is.True)
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
            var topLevelSegments = new[] { node1.Build(), node2.Build() };
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TrxName");
            var attributes = new AttributeValueCollection(AttributeDestinations.TransactionTrace);

            var trace = _transactionTraceMaker.GetTransactionTrace(transaction, topLevelSegments, transactionMetricName, attributes);

            var realSegments = trace.TransactionTraceData.RootSegment.Children.First().Children;
            var segment1 = realSegments.ElementAt(0);
            var segment2 = realSegments.ElementAt(1);
            var segment11 = segment1.Children.ElementAt(0);

            NrAssert.Multiple(
                () => Assert.That(segment1.Parameters["exclusive_duration_millis"], Is.EqualTo(750)),
                () => Assert.That(segment2.Parameters["exclusive_duration_millis"], Is.EqualTo(500)),
                () => Assert.That(segment11.Parameters["exclusive_duration_millis"], Is.EqualTo(250))
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
            var topLevelSegments = new[] { node1.Build(), node2.Build() };
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TrxName");
            var attributes = new AttributeValueCollection(AttributeDestinations.TransactionTrace);

            var trace = _transactionTraceMaker.GetTransactionTrace(transaction, topLevelSegments, transactionMetricName, attributes);

            var realSegments = trace.TransactionTraceData.RootSegment.Children.First().Children;
            var segment1 = realSegments.ElementAt(0);
            var segment2 = realSegments.ElementAt(1);
            var segment11 = segment1.Children.ElementAt(0);
            var segment12 = segment1.Children.ElementAt(1);
            var segment121 = segment12.Children.ElementAt(0);

            NrAssert.Multiple(
                () => Assert.That(segment1.Name, Is.EqualTo("1")),
                () => Assert.That(segment2.Name, Is.EqualTo("2")),
                () => Assert.That(segment11.Name, Is.EqualTo("1.1")),
                () => Assert.That(segment12.Name, Is.EqualTo("1.2")),
                () => Assert.That(segment121.Name, Is.EqualTo("1.2.1"))
                );
        }

        private static ImmutableSegmentTreeNode BuildNode(ImmutableTransaction transaction = null, DateTime? startTime = null, TimeSpan? duration = null, string name = "", MethodCallData methodCallData = null, IEnumerable<KeyValuePair<string, object>> parameters = null)
        {
            startTime = startTime ?? DateTime.Now;
            var relativeStart = startTime.Value - (transaction?.StartTime ?? startTime.Value);
            methodCallData = methodCallData ?? new MethodCallData("typeName", "methodName", 1);
            return new SegmentTreeNodeBuilder(SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(relativeStart, duration ?? TimeSpan.Zero, 2, 1, methodCallData, parameters ?? new Dictionary<string, object>(), name, false))
                .Build();
        }

        private ImmutableSegmentTreeNode BuildDataStoreSegmentNode(TimeSpan startTime = new TimeSpan(), TimeSpan? duration = null, string name = "", MethodCallData methodCallData = null, IEnumerable<KeyValuePair<string, object>> parameters = null)
        {
            methodCallData = methodCallData ?? new MethodCallData("typeName", "methodName", 1);

            var data = new DatastoreSegmentData(_databaseService, new ParsedSqlStatement(DatastoreVendor.MSSQL, "test_table", "SELECT"), "SELECT * FROM test_table");

            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), methodCallData);
            segment.SetSegmentData(data);

            return new SegmentTreeNodeBuilder(
                new Segment(startTime, duration ?? TimeSpan.Zero, segment, parameters))
                .Build();
        }

        private ImmutableSegmentTreeNode BuildDataStoreSegmentNodeWithInstanceData(TimeSpan startTime = new TimeSpan(), TimeSpan? duration = null, string name = "", MethodCallData methodCallData = null, IEnumerable<KeyValuePair<string, object>> parameters = null)
        {
            methodCallData = methodCallData ?? new MethodCallData("typeName", "methodName", 1);

            var data = new DatastoreSegmentData(_databaseService, new ParsedSqlStatement(DatastoreVendor.MSSQL, "test_table", "SELECT"),
                "SELECT * FROM test_table",
                new ConnectionInfo("My Vendor", "My Host", "My Port", "My Database"));

            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), methodCallData);
            segment.SetSegmentData(data);

            return new SegmentTreeNodeBuilder(new Segment(startTime, duration ?? TimeSpan.Zero, segment, null))
                .Build();
        }

        private static SegmentTreeNodeBuilder GetNodeBuilder(TimeSpan startTime = new TimeSpan(), TimeSpan? duration = null, string name = "", MethodCallData methodCallData = null, IEnumerable<KeyValuePair<string, object>> parameters = null)
        {
            methodCallData = methodCallData ?? new MethodCallData("typeName", "methodName", 1);
            return new SegmentTreeNodeBuilder(SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(startTime, duration ?? TimeSpan.Zero, 2, 1, methodCallData, parameters ?? new Dictionary<string, object>(), name, false));
        }

        private ImmutableTransaction BuildTestTransaction(DateTime? startTime = null, TimeSpan? duration = null, TimeSpan? responseTime = null, string uri = null, string guid = null)
        {
            var transactionMetadata = new TransactionMetadata(guid);
            if (uri != null)
                transactionMetadata.SetUri(uri);

            var name = TransactionName.ForWebTransaction("foo", "bar");
            var segments = Enumerable.Empty<Segment>();
            var metadata = transactionMetadata.ConvertToImmutableMetadata();
            startTime = startTime ?? DateTime.Now;
            duration = duration ?? TimeSpan.FromSeconds(1);
            guid = guid ?? Guid.NewGuid().ToString();

            return new ImmutableTransaction(name, segments, metadata, startTime.Value, duration.Value, responseTime, guid, false, false, false, 1.2f, false, string.Empty, null, _attribDefs);
        }
    }
}
