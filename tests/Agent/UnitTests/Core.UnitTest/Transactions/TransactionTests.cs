// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using Telerik.JustMock;
using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Segments;
using System.Collections.Generic;
using System.Data;
using NewRelic.Agent.Extensions.Parsing;

namespace NewRelic.Agent.Core.Transactions;

[TestFixture]
public class TransactionTests
{
    private Transaction _transaction;
    private IConfiguration _configuration;
    private IAttributeDefinitionService _attribDefSvc;
    private IAttributeDefinitions AttribDefs => _attribDefSvc.AttributeDefs;

    private readonly float _priority = 0.0f;
    private IDatabaseStatementParser _databaseStatementParser;
    private IDistributedTracePayloadHandler _distributedTracePayloadHandler;

    [SetUp]
    public void SetUp()
    {
        _configuration = Mock.Create<IConfiguration>();
        Mock.Arrange(() => _configuration.TransactionEventsEnabled).Returns(true);

        // Initialize the Transaction with its dependencies
        _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
        _databaseStatementParser = Mock.Create<IDatabaseStatementParser>();
        _distributedTracePayloadHandler = Mock.Create<IDistributedTracePayloadHandler>();

        _transaction = new Transaction(_configuration, Mock.Create<ITransactionName>(), Mock.Create<ISimpleTimer>(),
            DateTime.UtcNow, Mock.Create<ICallStackManager>(), Mock.Create<IDatabaseService>(),
            _priority, _databaseStatementParser, _distributedTracePayloadHandler, Mock.Create<IErrorService>(), AttribDefs);
    }

    [TearDown]
    public void TearDown()
    {
        _attribDefSvc.Dispose();
    }

    [Test]
    public void StartSegment_ReturnsSegment()
    {
        // Arrange
        var method = new Method(typeof(object), "TestMethod", "ParameterTypeNames");
        var methodCall = new MethodCall(method, new object(), Array.Empty<object>(), false);

        // Act
        var result = _transaction.StartSegment(methodCall);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<Segment>());
    }

    [Test]
    public void StartSegment_WhenTransactionIsIgnored_ReturnsNoOpSegment()
    {
        // Arrange
        var method = new Method(typeof(object), "TestMethod", "ParameterTypeNames");
        var methodCall = new MethodCall(method, new object(), Array.Empty<object>(), false);
        _transaction.Ignore();

        // Act
        var result = _transaction.StartSegment(methodCall);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<NoOpSegment>());
    }
    [Test]
    public void SetWebTransactionName_SetsTransactionName()
    {
        // Arrange
        var webTransactionType = WebTransactionType.Custom;
        var name = "TestTransaction";
        var priority = TransactionNamePriority.Uri;

        // Act
        _transaction.SetWebTransactionName(webTransactionType, name, priority);

        // Assert
        Assert.That(_transaction.CandidateTransactionName.CurrentTransactionName.Name, Is.EqualTo(name));
        Assert.That(_transaction.CandidateTransactionName.CurrentTransactionName.Category, Is.EqualTo(EnumNameCache<WebTransactionType>.GetName(webTransactionType)));
        Assert.That(_transaction.CandidateTransactionName.CurrentTransactionName.IsWeb, Is.True);
    }

    [Test]
    public void Transaction_SetRequestMethod_SetsRequestMethodAttribute()
    {
        // Arrange
        var requestMethod = "GET";

        // Act
        _transaction.SetRequestMethod(requestMethod);

        // Assert
        Assert.That(_transaction.TransactionMetadata.RequestMethod, Is.EqualTo(requestMethod));
    }

    [Test]
    public void SetUri_SetsUriInTransactionMetadata()
    {
        // Arrange
        var uri = "http://example.com";

        // Act
        _transaction.SetUri(uri);

        // Assert
        Assert.That(_transaction.TransactionMetadata.Uri, Is.EqualTo(uri));
    }

    [Test]
    public void SetUri_ThrowsArgumentNullException_WhenUriIsNull()
    {
        // Arrange
        string uri = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _transaction.SetUri(uri));
    }

    [Test]
    public void SetUri_CleansUriBeforeSetting()
    {
        // Arrange
        var uri = "http://example.com?param=value";
        var expectedCleanUri = "http://example.com";

        // Act
        _transaction.SetUri(uri);

        // Assert
        Assert.That(_transaction.TransactionMetadata.Uri, Is.EqualTo(expectedCleanUri));
    }
    [Test]
    public void SetOriginalUri_SetsOriginalUriInTransactionMetadata()
    {
        // Arrange
        var uri = "http://example.com";

        // Act
        _transaction.SetOriginalUri(uri);

        // Assert
        Assert.That(_transaction.TransactionMetadata.OriginalUri, Is.EqualTo(uri));
    }

    [Test]
    public void SetOriginalUri_ThrowsArgumentNullException_WhenUriIsNull()
    {
        // Arrange
        string uri = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _transaction.SetOriginalUri(uri));
    }

    [Test]
    public void SetReferrerUri_SetsReferrerUriInTransactionMetadata()
    {
        // Arrange
        var uri = "http://example.com";

        // Act
        _transaction.SetReferrerUri(uri);

        // Assert
        Assert.That(_transaction.TransactionMetadata.ReferrerUri, Is.EqualTo(uri));
    }

    [Test]
    public void SetReferrerUri_ThrowsArgumentNullException_WhenUriIsNull()
    {
        // Arrange
        string uri = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _transaction.SetReferrerUri(uri));
    }

    [Test]
    public void SetQueueTime_SetsQueueTimeInTransactionMetadata()
    {
        // Arrange
        var queueTime = TimeSpan.FromSeconds(5);

        // Act
        _transaction.SetQueueTime(queueTime);

        // Assert
        Assert.That(_transaction.TransactionMetadata.QueueTime, Is.EqualTo(queueTime));
    }

    [Test]
    public void SetRequestParameters_SetsRequestParametersInTransactionMetadata()
    {
        // Arrange
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("param1", "value1"),
            new("param2", "value2")
        };

        // Act
        _transaction.SetRequestParameters(parameters);

        // Assert
        var allAttributeValuesDic = _transaction.TransactionMetadata.UserAndRequestAttributes.GetAllAttributeValuesDic();
        foreach (var parameter in parameters)
        {
            var attributeValue = allAttributeValuesDic[$"request.parameters.{parameter.Key}"];
            Assert.That(attributeValue, Is.EqualTo(parameter.Value));
        }
    }

    [Test]
    public void SetRequestParameters_ThrowsArgumentNullException_WhenParametersIsNull()
    {
        // Arrange
        IEnumerable<KeyValuePair<string, string>> parameters = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _transaction.SetRequestParameters(parameters));
    }

    [Test]
    public void AddCustomAttribute_SetsCustomAttributeInTransactionMetadata()
    {
        // Arrange
        var key = "TestAttribute";
        var value = "TestValue";

        // Act
        _transaction.AddCustomAttribute(key, value);

        // Assert
        var allAttributeValuesDic = _transaction.TransactionMetadata.UserAndRequestAttributes.GetAllAttributeValuesDic();

        var attributeValue = allAttributeValuesDic[key];
        Assert.That(attributeValue, Is.EqualTo(value));
    }

    [Test]
    public void AddCustomAttribute_DoesNotSetAttribute_WhenKeyIsNull()
    {
        // Arrange
        string key = null;
        var value = "TestValue";

        // Act
        _transaction.AddCustomAttribute(key, value);

        // Assert
        Assert.That(_transaction.TransactionMetadata.UserAndRequestAttributes.Count, Is.EqualTo(0));
    }

    [Test]
    public void Add_ReturnsIndex_WhenSegmentIsNotNull()
    {
        // Arrange
        var segment = Mock.Create<Segment>();

        // Act
        var result = _transaction.Add(segment);

        // Assert
        Assert.That(result, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void Add_ReturnsMinusOne_WhenSegmentIsNull()
    {
        // Arrange
        Segment segment = null;

        // Act
        var result = _transaction.Add(segment);

        // Assert
        Assert.That(result, Is.EqualTo(-1));
    }

    [Test]
    public void Add_AddsSegmentToSegmentsCollection_WhenSegmentIsNotNull()
    {
        // Arrange
        var segment = Mock.Create<Segment>();

        // Act
        _transaction.Add(segment);

        // Assert
        Assert.That(_transaction.Segments, Has.Exactly(1).EqualTo(segment));
    }

    [Test]
    public void IgnoreAutoBrowserMonitoringForThisTx_SetsIgnoreAutoBrowserMonitoringToTrue()
    {
        // Act
        _transaction.IgnoreAutoBrowserMonitoringForThisTx();

        // Assert
        Assert.That(_transaction.IgnoreAutoBrowserMonitoring, Is.True);
    }

    [Test]
    public void GetTransactionSegmentState_ReturnsCurrentTransaction()
    {
        // Act
        var result = _transaction.GetTransactionSegmentState();

        // Assert
        Assert.That(result, Is.EqualTo(_transaction));
    }

    [Test]
    public void GetOrSetValueFromCache_ReturnsValueFromCache_WhenKeyExists()
    {
        // Arrange
        var key = "TestKey";
        var expectedValue = new object();
        //_transaction.TransactionCache[key] = expectedValue;

        // Act
        _ = _transaction.GetOrSetValueFromCache(key, () => expectedValue);

        var unexpectedValue = new object();
        var resultFromCache = _transaction.GetOrSetValueFromCache(key, () => unexpectedValue);

        // Assert
        Assert.That(resultFromCache, Is.EqualTo(expectedValue));
    }

    [Test]
    public void GetOrSetValueFromCache_ReturnsNewValue_WhenKeyDoesNotExist()
    {
        // Arrange
        var key = "TestKey";
        var expectedValue = new object();

        // Act
        var result = _transaction.GetOrSetValueFromCache(key, () => expectedValue);

        // Assert
        Assert.That(result, Is.EqualTo(expectedValue));
    }

    [Test]
    public void GetOrSetValueFromCache_ReturnsNull_WhenKeyIsNull()
    {
        // Arrange
        string key = null;

        // Act
        var result = _transaction.GetOrSetValueFromCache(key, () => new object());

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ForceChangeDuration_SetsForcedDuration()
    {
        // Arrange
        var expectedDuration = TimeSpan.FromSeconds(5);

        // Act
        _transaction.ForceChangeDuration(expectedDuration);
        var immutableTransaction = _transaction.ConvertToImmutableTransaction();

        // Assert
        var actualDuration = immutableTransaction.Duration;
        Assert.That(actualDuration, Is.EqualTo(expectedDuration));
    }

    [Test]
    public void GetParsedDatabaseStatement_ReturnsParsedStatement_WhenSqlIsValid()
    {
        // Arrange
        var datastoreVendor = DatastoreVendor.MSSQL;
        var commandType = CommandType.Text;
        var sql = "SELECT * FROM TestTable";

        Mock.Arrange(() => _databaseStatementParser.ParseDatabaseStatement(datastoreVendor, commandType, sql))
            .Returns(new ParsedSqlStatement(datastoreVendor, "TestTable", "SELECT"));

        // Act
        var result = _transaction.GetParsedDatabaseStatement(datastoreVendor, commandType, sql);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Model, Is.EqualTo("TestTable"));
        Assert.That(result.Operation, Is.EqualTo("SELECT"));
    }

    [Test]
    public void SetRequestHeaders_ThrowsArgumentNullException_WhenHeadersIsNull()
    {
        // Arrange
        var keysToCapture = new List<string> { "key1", "key2" };
        Func<object, string, string> getter = (obj, key) => "value";

        // Act
        void Action() => _transaction.SetRequestHeaders<object>(null, keysToCapture, getter);

        // Assert
        Assert.That(Action, Throws.ArgumentNullException);
    }

    [Test]
    public void SetRequestHeaders_ThrowsArgumentNullException_WhenKeysToCaptureIsNull()
    {
        // Arrange
        var headers = new object();
        Func<object, string, string> getter = (obj, key) => "value";

        // Act
        void Action() => _transaction.SetRequestHeaders(headers, null, getter);

        // Assert
        Assert.That(Action, Throws.ArgumentNullException);
    }

    [Test]
    public void SetRequestHeaders_ThrowsArgumentNullException_WhenGetterIsNull()
    {
        // Arrange
        var headers = new object();
        var keysToCapture = new List<string> { "key1", "key2" };

        // Act
        void Action() => _transaction.SetRequestHeaders(headers, keysToCapture, null);

        // Assert
        Assert.That(Action, Throws.ArgumentNullException);
    }

    [Test]
    public void SetRequestHeaders_SetsRequestHeaders_WhenParametersAreValid()
    {
        // Arrange
        var headers = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
        var keysToCapture = new List<string> { "key1", "key2" };
        string Getter(Dictionary<string, string> dict, string key) => dict.TryGetValue(key, out var value) ? value : null;

        // Act
        _transaction.SetRequestHeaders(headers, keysToCapture, Getter);

        // Assert
        var allAttributeValuesDic = _transaction.TransactionMetadata.UserAndRequestAttributes.GetAllAttributeValuesDic();
        foreach (var header in headers)
        {
            var attributeValue = allAttributeValuesDic[$"request.headers.{header.Key}"];
            Assert.That(attributeValue, Is.EqualTo(header.Value));
        }
    }

    [Test]
    public void InsertDistributedTraceHeaders_InvokesDistributedTracePayloadHandler_WhenDistributedTracingEnabled()
    {
        // Arrange
        var carrier = new Dictionary<string, string>();
        Action<Dictionary<string, string>, string, string> setter = (dict, key, value) => dict[key] = value;
        Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);

        // Act
        _transaction.InsertDistributedTraceHeaders(carrier, setter);

        // Assert
        Mock.Assert(() => _distributedTracePayloadHandler.InsertDistributedTraceHeaders(_transaction, carrier, setter));
    }
    [Test]
    public void TransactionMetadata_IsLlmTransaction_ReturnsFalse_WhenLlmTransactionIsNotSet()
    {
        // Act
        var result = _transaction.TransactionMetadata.IsLlmTransaction;

        // Assert
        Assert.That(result, Is.False);
    }
    [Test]
    public void TransactionMetadata_IsLlmTransaction_ReturnsTrue_WhenLlmTransactionIsSet()
    {
        // Act
        _transaction.SetLlmTransaction(true);

        // Assert
        Assert.That(_transaction.TransactionMetadata.IsLlmTransaction, Is.True);
    }

    [Test]
    public void AddLambdaAttribute_SetAttributeInTransactionMetadata()
    {
        // Arrange
        var key = "TestAttribute";
        var value = "TestValue";

        // Act
        _transaction.AddLambdaAttribute(key, value);

        // Assert
        var allAttributeValuesDic = _transaction.TransactionMetadata.UserAndRequestAttributes.GetAllAttributeValuesDic();

        var attributeValue = allAttributeValuesDic[key];
        Assert.That(attributeValue, Is.EqualTo(value));
    }

    [TestCase("   ")]
    [TestCase("")]
    [TestCase(null)]
    public void AddLambdaAttribute_DoesNotSetAttribute_WhenKeyIsBad(string key)
    {
        // Arrange
        var value = "TestValue";

        // Act
        _transaction.AddLambdaAttribute(key, value);

        // Assert
        Assert.That(_transaction.TransactionMetadata.UserAndRequestAttributes.Count, Is.EqualTo(0));
    }
}
