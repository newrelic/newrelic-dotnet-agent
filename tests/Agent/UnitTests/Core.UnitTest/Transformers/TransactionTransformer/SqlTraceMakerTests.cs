// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer.UnitTest
{
    [TestFixture]
    public class SqlTraceMakerTests
    {
        private IDatabaseService _databaseService;

        private IConfigurationService _configurationService;

        private SqlTraceMaker _sqlTraceMaker;

        private IErrorService _errorService;

        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

        [SetUp]
        public void SetUp()
        {
            _databaseService = Mock.Create<IDatabaseService>();
            Mock.Arrange(() => _databaseService.GetObfuscatedSql(Arg.AnyString, Arg.IsAny<DatastoreVendor>())).Returns((string sql) => sql);
            _configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => _configurationService.Configuration.InstanceReportingEnabled).Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.DatabaseNameReportingEnabled).Returns(true);

            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
            _sqlTraceMaker = new SqlTraceMaker(_configurationService, _attribDefSvc, _databaseService);
            _errorService = new ErrorService(_configurationService);
        }

        [Test]
        public void TryGetSqlTrace_ReturnsTrace()
        {
            var uri = "sqlTrace/Uri";
            var commandText = "Select * from Table1";
            var duration = TimeSpan.FromMilliseconds(500);
            var transaction = BuildTestTransaction(uri);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "Name");
            var datastoreSegment = BuildSegment(DatastoreVendor.MSSQL, "Table1", commandText, new TimeSpan(), duration, null, null, null, "myhost", "myport", "mydatabase");

            var sqlTrace = _sqlTraceMaker.TryGetSqlTrace(transaction, transactionMetricName, datastoreSegment);
            Assert.IsNotNull(sqlTrace);
            Assert.AreEqual(commandText, sqlTrace.Sql);
            Assert.AreEqual(uri, sqlTrace.Uri);
            Assert.AreEqual(duration, sqlTrace.TotalCallTime);
            Assert.AreEqual(3, sqlTrace.ParameterData.Count); // Explain plans will go here
            Assert.AreEqual("myhost", sqlTrace.ParameterData["host"]);
            Assert.AreEqual("myport", sqlTrace.ParameterData["port_path_or_id"]);
            Assert.AreEqual("mydatabase", sqlTrace.ParameterData["database_name"]);
            Assert.AreEqual("WebTransaction/Name", sqlTrace.TransactionName);
        }

        [Test]
        public void TryGetSqlTrace_ReturnsNullWhenDurationIsNull()
        {
            var uri = "sqlTrace/Uri";
            var commandText = "Select * from Table1";
            var transaction = BuildTestTransaction(uri);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "Name");
            var datastoreSegment = BuildSegment(DatastoreVendor.MSSQL, "Table1", commandText, new TimeSpan(), null);

            var sqlTrace = _sqlTraceMaker.TryGetSqlTrace(transaction, transactionMetricName, datastoreSegment);
            Assert.IsNull(sqlTrace);
        }

        [Test]
        public void SqlTrace_WithoutUri()
        {
            var commandText = "Select * from Table1";
            var duration = TimeSpan.FromMilliseconds(500);
            var transaction = BuildTestTransaction();
            var transactionMetricName = new TransactionMetricName("WebTransaction", "Name");
            var datastoreSegment = BuildSegment(DatastoreVendor.MSSQL, "Table1", commandText, new TimeSpan(), duration, null, null, null, "myhost", "myport", "mydatabase");

            var sqlTrace = _sqlTraceMaker.TryGetSqlTrace(transaction, transactionMetricName, datastoreSegment);
            Assert.IsNotNull(sqlTrace);
            Assert.AreEqual("<unknown>", sqlTrace.Uri);
        }

        [Test]
        public void SqlTrace_WithtUriExcluded()
        {
            //Arrange
            var attribDefs = Mock.Create<IAttributeDefinitions>();
            var attribDefSvc = new AttributeDefinitionService((f) => attribDefs);
            var sqlTraceMaker = new SqlTraceMaker(_configurationService, attribDefSvc, _databaseService);
            var attribFilter = Mock.Create<IAttributeFilter>();

            Mock.Arrange(()=>attribDefs.RequestUri)
                .Returns(AttributeDefinitionBuilder.CreateString("request.uri", AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.TransactionEvent)
                .AppliesTo(AttributeDestinations.ErrorEvent)
                .AppliesTo(AttributeDestinations.ErrorTrace)
                .AppliesTo(AttributeDestinations.TransactionTrace)
                .AppliesTo(AttributeDestinations.SqlTrace, false)
                .WithDefaultOutputValue("/unknown")
                .Build(attribFilter));

            var uri = "sqlTrace/Uri";
            var commandText = "Select * from Table1";
            var duration = TimeSpan.FromMilliseconds(500);
            var transaction = BuildTestTransaction(uri: uri, attribDefs: attribDefs);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "Name");
            var datastoreSegment = BuildSegment(DatastoreVendor.MSSQL, "Table1", commandText, new TimeSpan(), duration, null, null, null, "myhost", "myport", "mydatabase");

            //Act
            var sqlTrace = sqlTraceMaker.TryGetSqlTrace(transaction, transactionMetricName, datastoreSegment);

            //Assert
            Assert.IsNotNull(sqlTrace);
            Assert.AreEqual("<unknown>", sqlTrace.Uri);
        }

        private ImmutableTransaction BuildTestTransaction(string uri = null, string guid = null, int? statusCode = null, int? subStatusCode = null, IEnumerable<ErrorData> transactionExceptionDatas = null, IAttributeDefinitions attribDefs = null)
        {
            var txMetadata = new TransactionMetadata();
            if (uri != null)
                txMetadata.SetUri(uri);
            if (statusCode != null)
                txMetadata.SetHttpResponseStatusCode(statusCode.Value, subStatusCode, _errorService);
            if (transactionExceptionDatas != null)
                transactionExceptionDatas.ForEach(data => txMetadata.TransactionErrorState.AddExceptionData(data));

            var name = TransactionName.ForWebTransaction("foo", "bar");
            var segments = Enumerable.Empty<Segment>();
            var immutableMetadata = txMetadata.ConvertToImmutableMetadata();
            guid = guid ?? Guid.NewGuid().ToString();

            return new ImmutableTransaction(name, segments, immutableMetadata, DateTime.UtcNow, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), guid, false, false, false, 0.5f, false, string.Empty, null, attribDefs ?? _attribDefs);
        }

        private Segment BuildSegment(DatastoreVendor vendor, string model, string commandText, TimeSpan startTime = new TimeSpan(), TimeSpan? duration = null, string name = "", MethodCallData methodCallData = null, IEnumerable<KeyValuePair<string, object>> parameters = null, string host = null, string portPathOrId = null, string databaseName = null)
        {
            var data = new DatastoreSegmentData(_databaseService, new ParsedSqlStatement(vendor, model, null), commandText,
                new ConnectionInfo(host, portPathOrId, databaseName));
            methodCallData = methodCallData ?? new MethodCallData("typeName", "methodName", 1);

            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), methodCallData);
            segment.SetSegmentData(data);

            return new Segment(startTime, duration, segment, parameters);
        }
    }
}
