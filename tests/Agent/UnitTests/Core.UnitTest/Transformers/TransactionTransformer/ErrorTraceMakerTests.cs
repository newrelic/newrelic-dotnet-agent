using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
    [TestFixture]
    public class ErrorTraceMakerTests
    {
        private IConfiguration _configuration;
        private IConfigurationService _configurationService;
        private ErrorTraceMaker _errorTraceMaker;

        [SetUp]
        public void SetUp()
        {
            _configuration = Mock.Create<IConfiguration>();
            _configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);

            var attributeService = Mock.Create<IAttributeService>();
            Mock.Arrange(() => attributeService.FilterAttributes(Arg.IsAny<Attributes>(), Arg.IsAny<AttributeDestinations>())).Returns<Attributes, AttributeDestinations>((attrs, _) => attrs);

            _errorTraceMaker = new ErrorTraceMaker(_configurationService, attributeService);
        }

        [Test]
        public void GetErrorTrace_ReturnsErrorTrace_IfStatusCodeIs404()
        {
            var transaction = BuildTestTransaction(statusCode: 404, uri: "http://www.newrelic.com/test?param=value");
            var attributes = new Attributes();
            var transactionMetricName = new TransactionMetricName("WebTransaction", "Name");

            var errorData = ErrorData.TryGetErrorData(transaction, _configurationService);
            var errorTrace = _errorTraceMaker.GetErrorTrace(transaction, attributes, transactionMetricName, errorData);

            Assert.NotNull(errorTrace);
            NrAssert.Multiple(
                () => Assert.AreEqual("WebTransaction/Name", errorTrace.Path),
                () => Assert.AreEqual("Not Found", errorTrace.Message),
                () => Assert.AreEqual("404", errorTrace.ExceptionClassName),
                () => Assert.AreEqual(transaction.Guid, errorTrace.Guid),
                () => Assert.AreEqual("http://www.newrelic.com/test", errorTrace.Attributes.RequestUri),
                () => Assert.AreEqual(null, errorTrace.Attributes.StackTrace)
                );
        }

        [Test]
        public void GetErrorTrace_ReturnsErrorTrace_IfExceptionIsNoticed()
        {
            var errorDataIn = ErrorData.FromParts("My message", "My type name", DateTime.UtcNow, false);
            var transaction = BuildTestTransaction(uri: "http://www.newrelic.com/test?param=value", transactionExceptionDatas: new[] { errorDataIn });
            var attributes = new Attributes();
            var transactionMetricName = new TransactionMetricName("WebTransaction", "Name");

            var errorDataOut = ErrorData.TryGetErrorData(transaction, _configurationService);
            var errorTrace = _errorTraceMaker.GetErrorTrace(transaction, attributes, transactionMetricName, errorDataOut);

            Assert.NotNull(errorTrace);
            NrAssert.Multiple(
                () => Assert.AreEqual("WebTransaction/Name", errorTrace.Path),
                () => Assert.AreEqual("My message", errorTrace.Message),
                () => Assert.AreEqual("My type name", errorTrace.ExceptionClassName),
                () => Assert.AreEqual(transaction.Guid, errorTrace.Guid),
                () => Assert.AreEqual("http://www.newrelic.com/test", errorTrace.Attributes.RequestUri)
            );
        }

        [Test]
        public void GetErrorTrace_ReturnsFirstException_IfMultipleExceptionsNoticed()
        {
            var errorData1 = ErrorData.FromParts("My message", "My type name", DateTime.UtcNow, false);
            var errorData2 = ErrorData.FromParts("My message2", "My type name2", DateTime.UtcNow, false);
            var transaction = BuildTestTransaction(uri: "http://www.newrelic.com/test?param=value", transactionExceptionDatas: new[] { errorData1, errorData2 });
            var attributes = new Attributes();
            var transactionMetricName = new TransactionMetricName("WebTransaction", "Name");

            var errorDataOut = ErrorData.TryGetErrorData(transaction, _configurationService);
            var errorTrace = _errorTraceMaker.GetErrorTrace(transaction, attributes, transactionMetricName, errorDataOut);

            Assert.NotNull(errorTrace);
            NrAssert.Multiple(
                () => Assert.AreEqual("WebTransaction/Name", errorTrace.Path),
                () => Assert.AreEqual("My message", errorTrace.Message),
                () => Assert.AreEqual("My type name", errorTrace.ExceptionClassName),
                () => Assert.AreEqual(transaction.Guid, errorTrace.Guid),
                () => Assert.AreEqual("http://www.newrelic.com/test", errorTrace.Attributes.RequestUri)
            );
        }

        [Test]
        public void GetErrorTrace_ReturnsExceptionsBeforeStatusCodes()
        {
            var errorDataIn = ErrorData.FromParts("My message", "My type name", DateTime.UtcNow, false);
            var transaction = BuildTestTransaction(statusCode: 404, uri: "http://www.newrelic.com/test?param=value", transactionExceptionDatas: new[] { errorDataIn });
            var attributes = new Attributes();
            var transactionMetricName = new TransactionMetricName("WebTransaction", "Name");

            var errorDataOut = ErrorData.TryGetErrorData(transaction, _configurationService);
            var errorTrace = _errorTraceMaker.GetErrorTrace(transaction, attributes, transactionMetricName, errorDataOut);

            Assert.NotNull(errorTrace);
            NrAssert.Multiple(
                () => Assert.AreEqual("WebTransaction/Name", errorTrace.Path),
                () => Assert.AreEqual("My message", errorTrace.Message),
                () => Assert.AreEqual("My type name", errorTrace.ExceptionClassName),
                () => Assert.AreEqual(transaction.Guid, errorTrace.Guid),
                () => Assert.AreEqual("http://www.newrelic.com/test", errorTrace.Attributes.RequestUri)
            );
        }
        private static ImmutableTransaction BuildTestTransaction(String uri = null, String guid = null, Int32? statusCode = null, Int32? subStatusCode = null, IEnumerable<ErrorData> transactionExceptionDatas = null)
        {
            var transactionMetadata = new TransactionMetadata();
            if (uri != null)
                transactionMetadata.SetUri(uri);
            if (statusCode != null)
                transactionMetadata.SetHttpResponseStatusCode(statusCode.Value, subStatusCode);
            if (transactionExceptionDatas != null)
                transactionExceptionDatas.ForEach(data => transactionMetadata.AddExceptionData(data));

            var name = new WebTransactionName("foo", "bar");
            var segments = Enumerable.Empty<Segment>();
            var metadata = transactionMetadata.ConvertToImmutableMetadata();
            guid = guid ?? Guid.NewGuid().ToString();

            return new ImmutableTransaction(name, segments, metadata, DateTime.UtcNow, TimeSpan.FromSeconds(1), guid, false, false, false, SqlObfuscator.GetObfuscatingSqlObfuscator());
        }
    }
}
