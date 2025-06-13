// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Segments;

[TestFixture]
public class DatastoreSegmentDataTests
{
    private IDatabaseService _databaseService;
    private ParsedSqlStatement _parsedSqlStatement;
    private ConnectionInfo _connectionInfo;
    private DatastoreVendor _datastoreVendor;

    [SetUp]
    public void SetUp()
    {
        _databaseService = Mock.Create<IDatabaseService>();
        _datastoreVendor = DatastoreVendor.Other;
        _parsedSqlStatement = new ParsedSqlStatement(_datastoreVendor, "model", "operation");
        _connectionInfo = new ConnectionInfo("host", "portPathOrId", "databaseName");
    }

    [TearDown]
    public void TearDown()
    {
        DatastoreSegmentData.ClearFailedExplainPlanCache();
        _databaseService.Dispose();
    }

    [Test]
    public void Constructor_InitializesProperties()
    {
        var commandText = "SELECT * FROM table";
        var queryParameters = new Dictionary<string, IConvertible> { { "param1", "value1" } };

        var segmentData = new DatastoreSegmentData(_databaseService, _parsedSqlStatement, commandText, _connectionInfo, queryParameters);

        Assert.Multiple(() =>
        {
            Assert.That(segmentData.CommandText, Is.EqualTo(commandText));
            Assert.That(segmentData.QueryParameters, Is.EqualTo(queryParameters));
            Assert.That(segmentData.Host, Is.EqualTo(_connectionInfo.Host));
            Assert.That(segmentData.Port, Is.EqualTo(_connectionInfo.Port));
            Assert.That(segmentData.PathOrId, Is.EqualTo(_connectionInfo.PathOrId));
            Assert.That(segmentData.DatabaseName, Is.EqualTo(_connectionInfo.DatabaseName));
            Assert.That(segmentData.Operation, Is.EqualTo(_parsedSqlStatement.Operation));
            Assert.That(segmentData.DatastoreVendorName, Is.EqualTo(_parsedSqlStatement.DatastoreVendor));
            Assert.That(segmentData.Model, Is.EqualTo(_parsedSqlStatement.Model));
        });
    }

    [Test]
    public void IsCombinableWith_ReturnsTrueForSameOperationAndVendor()
    {
        var segmentData1 = new DatastoreSegmentData(_databaseService, _parsedSqlStatement);
        var segmentData2 = new DatastoreSegmentData(_databaseService, _parsedSqlStatement);

        var result = segmentData1.IsCombinableWith(segmentData2);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsCombinableWith_ReturnsFalseForDifferentOperation()
    {
        var segmentData1 = new DatastoreSegmentData(_databaseService, _parsedSqlStatement);
        var segmentData2 = new DatastoreSegmentData(_databaseService, new ParsedSqlStatement(DatastoreVendor.Other, "model", "different_operation"));

        var result = segmentData1.IsCombinableWith(segmentData2);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsCombinableWith_ReturnsFalseForDifferentVendor()
    {
        var segmentData1 = new DatastoreSegmentData(_databaseService, _parsedSqlStatement);
        var segmentData2 = new DatastoreSegmentData(_databaseService, new ParsedSqlStatement(DatastoreVendor.MongoDB, "model", "operation"));

        var result = segmentData1.IsCombinableWith(segmentData2);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsCombinableWith_ReturnsFalseForDifferentModel()
    {
        var segmentData1 = new DatastoreSegmentData(_databaseService, _parsedSqlStatement);
        var segmentData2 = new DatastoreSegmentData(_databaseService, new ParsedSqlStatement(DatastoreVendor.Other, "different_model", "operation"));

        var result = segmentData1.IsCombinableWith(segmentData2);

        Assert.That(result, Is.False);
    }

    [Test]
    public void GetTransactionTraceName_ReturnsExpectedName()
    {
        var segmentData = new DatastoreSegmentData(_databaseService, _parsedSqlStatement);

        var result = segmentData.GetTransactionTraceName();

        Assert.That(result, Is.EqualTo("Datastore/statement/Other/model/operation"));
    }

    [Test]
    public void SetConnectionInfo_UpdatesConnectionInfo()
    {
        var segmentData = new DatastoreSegmentData(_databaseService, _parsedSqlStatement);
        var newConnectionInfo = new ConnectionInfo("new_host", "new_portPathOrId", "new_databaseName");

        segmentData.SetConnectionInfo(newConnectionInfo);

        Assert.Multiple(() =>
        {
            Assert.That(segmentData.Host, Is.EqualTo(newConnectionInfo.Host));
            Assert.That(segmentData.PortPathOrId, Is.EqualTo(newConnectionInfo.PortPathOrId));
            Assert.That(segmentData.DatabaseName, Is.EqualTo(newConnectionInfo.DatabaseName));
        });
    }

    [Test]
    public void ExecuteExplainPlan_ThrowsExceptionAndCachesQuery()
    {
        var query = "SELECT * FROM table";
        var segmentData = new DatastoreSegmentData(_databaseService, _parsedSqlStatement, query);
        var obfuscator = Mock.Create<SqlObfuscator>();

        segmentData.DoExplainPlanCondition = () => true;
        segmentData.GenerateExplainPlan = _ => throw new Exception("Test exception");

        Mock.Arrange(() => obfuscator.GetObfuscatedSql(Arg.IsAny<string>(), Arg.IsAny<DatastoreVendor>()))
            .Returns("obfuscated_sql");

        // First call to ExecuteExplainPlan should catch the exception and cache the query
        var success = segmentData.ExecuteExplainPlan(obfuscator);
        Assert.That(success, Is.False);
        Assert.That(() => segmentData.GetFailedExplainPlanCache().Contains(_datastoreVendor, query));

        // Second call to ExecuteExplainPlan should not attempt to generate the explain plan again
        segmentData.GenerateExplainPlan = _ => new ExplainPlan([], [], []); // reset so it doesn't throw if called
        success = segmentData.ExecuteExplainPlan(obfuscator);
        Assert.That(success, Is.False);
    }

    [Test]
    public void ExecuteExplainPlan_DoesNotReRun_WhenExplainPlan_IsNotNull()
    {
        var query = "SELECT * FROM table";
        var segmentData = new DatastoreSegmentData(_databaseService, _parsedSqlStatement, query);
        var obfuscator = Mock.Create<SqlObfuscator>();

        segmentData.DoExplainPlanCondition = () => true;
        segmentData.GenerateExplainPlan = _ => new ExplainPlan(new List<string>(), new List<List<object>>(), new List<int>());

        Mock.Arrange(() => obfuscator.GetObfuscatedSql(Arg.IsAny<string>(), Arg.IsAny<DatastoreVendor>()))
            .Returns("obfuscated_sql");

        // First call to ExecuteExplainPlan should generate the explain plan
        var result = segmentData.ExecuteExplainPlan(obfuscator);
        Assert.That(result, Is.True);

        // Verify that the explain plan is not null
        Assert.That(segmentData.ExplainPlan, Is.Not.Null);

        // Second call to ExecuteExplainPlan should not attempt to generate the explain plan again
        segmentData.GenerateExplainPlan = _ => throw new Exception("Test exception");
        result = segmentData.ExecuteExplainPlan(obfuscator);
        Assert.That(result, Is.False);

        // Verify that the explain plan is still not null
        Assert.That(segmentData.ExplainPlan, Is.Not.Null);
    }
}
