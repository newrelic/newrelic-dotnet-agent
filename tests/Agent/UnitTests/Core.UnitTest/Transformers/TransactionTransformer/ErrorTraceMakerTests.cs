/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer.UnitTest
{
    [TestFixture]
    public class ErrorTraceMakerTests
    {
        private IConfigurationService _configurationService;
        private ErrorTraceMaker _errorTraceMaker;
        private ErrorService _errorService;
        private AttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;

        private const string StripExceptionMessagesMessage = "Message removed by New Relic based on your currently enabled security settings.";

        [SetUp]
        public void SetUp()
        {
            _configurationService = Mock.Create<IConfigurationService>();

            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));

            _errorTraceMaker = new ErrorTraceMaker(_configurationService);
            _errorService = new ErrorService(_configurationService);
        }

        [Test]
        public void GetErrorTrace_ReturnsErrorTrace_IfStatusCodeIs404()
        {
            var transaction = BuildTestTransaction(statusCode: 404, uri: "http://www.newrelic.com/test?param=value");
            var attributes = new AttributeValueCollection(AttributeDestinations.ErrorTrace);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "Name");

            var errorTrace = _errorTraceMaker.GetErrorTrace(transaction, attributes, transactionMetricName);

            Assert.NotNull(errorTrace);
            NrAssert.Multiple(
                () => Assert.AreEqual("WebTransaction/Name", errorTrace.Path),
                () => Assert.AreEqual("Not Found", errorTrace.Message),
                () => Assert.AreEqual("404", errorTrace.ExceptionClassName),
                () => Assert.AreEqual(transaction.Guid, errorTrace.Guid),
                () => Assert.AreEqual(null, errorTrace.Attributes.StackTrace)
                );
        }

        [Test]
        public void GetErrorTrace_ReturnsErrorTrace_IfExceptionIsNoticed()
        {
            var errorDataIn = _errorService.FromMessage("My message", (Dictionary<string, object>)null);
            var transaction = BuildTestTransaction(uri: "http://www.newrelic.com/test?param=value", transactionExceptionDatas: new[] { errorDataIn });
            var attributes = new AttributeValueCollection(AttributeDestinations.ErrorTrace);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "Name");

            var errorTrace = _errorTraceMaker.GetErrorTrace(transaction, attributes, transactionMetricName);

            Assert.NotNull(errorTrace);
            NrAssert.Multiple(
                () => Assert.AreEqual("WebTransaction/Name", errorTrace.Path),
                () => Assert.AreEqual("My message", errorTrace.Message),
                () => Assert.AreEqual("Custom Error", errorTrace.ExceptionClassName),
                () => Assert.AreEqual(transaction.Guid, errorTrace.Guid)
            );
        }

        [Test]
        public void GetErrorTrace_ReturnsFirstException_IfMultipleExceptionsNoticed()
        {
            var errorData1 = _errorService.FromMessage("My message", (Dictionary<string, object>)null);
            var errorData2 = _errorService.FromMessage("My message2", (Dictionary<string, object>)null);
            var transaction = BuildTestTransaction(uri: "http://www.newrelic.com/test?param=value", transactionExceptionDatas: new[] { errorData1, errorData2 });
            var attributes = new AttributeValueCollection(AttributeDestinations.ErrorTrace);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "Name");

            var errorTrace = _errorTraceMaker.GetErrorTrace(transaction, attributes, transactionMetricName);

            Assert.NotNull(errorTrace);
            NrAssert.Multiple(
                () => Assert.AreEqual("WebTransaction/Name", errorTrace.Path),
                () => Assert.AreEqual("My message", errorTrace.Message),
                () => Assert.AreEqual("Custom Error", errorTrace.ExceptionClassName),
                () => Assert.AreEqual(transaction.Guid, errorTrace.Guid)
            );
        }

        [Test]
        public void GetErrorTrace_ReturnsExceptionsBeforeStatusCodes()
        {
            var errorDataIn = _errorService.FromMessage("My message", (Dictionary<string, object>)null);
            var transaction = BuildTestTransaction(statusCode: 404, uri: "http://www.newrelic.com/test?param=value", transactionExceptionDatas: new[] { errorDataIn });
            var attributes = new AttributeValueCollection(AttributeDestinations.ErrorTrace);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "Name");

            var errorTrace = _errorTraceMaker.GetErrorTrace(transaction, attributes, transactionMetricName);

            Assert.NotNull(errorTrace);
            NrAssert.Multiple(
                () => Assert.AreEqual("WebTransaction/Name", errorTrace.Path),
                () => Assert.AreEqual("My message", errorTrace.Message),
                () => Assert.AreEqual("Custom Error", errorTrace.ExceptionClassName),
                () => Assert.AreEqual(transaction.Guid, errorTrace.Guid)
            );
        }

        [Test]
        public void GetErrorTrace_ReturnsExceptionWithoutMessage_IfStripExceptionMessageEnabled()
        {
            Mock.Arrange(() => _configurationService.Configuration.StripExceptionMessages).Returns(true);

            var errorData = _errorService.FromMessage("This message should be stripped.", (Dictionary<string, object>)null);
            var transaction = BuildTestTransaction(uri: "http://www.newrelic.com/test?param=value", transactionExceptionDatas: new[] { errorData });
            var attributes = new AttributeValueCollection(AttributeDestinations.ErrorTrace);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "Name");

            var errorTrace = _errorTraceMaker.GetErrorTrace(transaction, attributes, transactionMetricName);

            Assert.NotNull(errorTrace);
            NrAssert.Multiple(
                () => Assert.AreEqual("WebTransaction/Name", errorTrace.Path),
                () => Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message),
                () => Assert.AreEqual("Custom Error", errorTrace.ExceptionClassName),
                () => Assert.AreEqual(transaction.Guid, errorTrace.Guid)
            );
        }

        private ImmutableTransaction BuildTestTransaction(string uri = null, string guid = null, int? statusCode = null, int? subStatusCode = null, IEnumerable<ErrorData> transactionExceptionDatas = null)
        {
            var transactionMetadata = new TransactionMetadata();
            if (uri != null)
                transactionMetadata.SetUri(uri);
            if (statusCode != null)
                transactionMetadata.SetHttpResponseStatusCode(statusCode.Value, subStatusCode, _errorService);
            if (transactionExceptionDatas != null)
                transactionExceptionDatas.ForEach(data => transactionMetadata.TransactionErrorState.AddExceptionData(data));

            var name = TransactionName.ForWebTransaction("foo", "bar");
            var segments = Enumerable.Empty<Segment>();
            var metadata = transactionMetadata.ConvertToImmutableMetadata();
            guid = guid ?? Guid.NewGuid().ToString();

            var attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));

            return new ImmutableTransaction(name, segments, metadata, DateTime.UtcNow, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), guid, false, false, false, 0.5f, false, string.Empty, null, attribDefSvc.AttributeDefs);
        }
    }
}
