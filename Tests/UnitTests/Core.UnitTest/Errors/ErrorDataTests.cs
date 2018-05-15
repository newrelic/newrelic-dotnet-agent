using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using MoreLinq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Errors.UnitTest
{
	[TestFixture]
	public class ErrorDataTests
	{
		[NotNull]
		private IConfigurationService _configurationService;

		private IConfiguration _configuration;

		[SetUp]
		public void SetUp()
		{
			_configurationService = Mock.Create<IConfigurationService>();
			_configuration = Mock.Create<IConfiguration>();
			Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);
		}

		[Test]
		public void TryGetErrorData_ReturnsEmpty_IfNoErrors()
		{
			var transaction = BuildTestTransaction(DateTime.UtcNow, TimeSpan.FromSeconds(1));

			var errorData = ErrorData.TryGetErrorData(transaction, _configurationService);

			Assert.Null(errorData.ErrorTypeName);
		}

		[Test]
		public void TryGetErrorData_ReturnsEmpty_IfNoErrorsAndStatusCodeIs200()
		{
			var transaction = BuildTestTransaction(DateTime.UtcNow, TimeSpan.FromSeconds(1), statusCode: 200);

			var errorData = ErrorData.TryGetErrorData(transaction, _configurationService);

			Assert.Null(errorData.ErrorTypeName);
		}

		[Test]
		public void TryGetErrorData_ReturnsErrorData_IfStatusCodeIs404()
		{
			var startTime = DateTime.UtcNow;
			var duration = TimeSpan.FromSeconds(1);

			var transaction = BuildTestTransaction(startTime, duration, statusCode: 404, uri: "http://www.newrelic.com/test?param=value");

			var errorData = ErrorData.TryGetErrorData(transaction, _configurationService);

			Assert.NotNull(errorData.ErrorTypeName);
			NrAssert.Multiple(
				() => Assert.AreEqual("404", errorData.ErrorTypeName),
				() => Assert.AreEqual("Not Found", errorData.ErrorMessage),
				() => Assert.AreEqual(startTime + duration, errorData.NoticedAt)
				);
		}

		[Test]
		public void TryGetErrorData_ReturnsErrorTrace_IfStatusCodeIs404AndSubstatusCodeIs5()
		{
			var startTime = DateTime.UtcNow;
			var duration = TimeSpan.FromSeconds(1);
			var transaction = BuildTestTransaction(startTime, duration, statusCode: 404, subStatusCode: 5, uri: "http://www.newrelic.com/test?param=value");

			var errorData = ErrorData.TryGetErrorData(transaction, _configurationService);

			NrAssert.Multiple(
				() => Assert.AreEqual("404.5", errorData.ErrorTypeName),
				() => Assert.AreEqual("Not Found", errorData.ErrorMessage),
				() => Assert.AreEqual(startTime + duration, errorData.NoticedAt)
				);
		}

		[Test]
		public void TryGetErrorData_ReturnsErrorTrace_IfExceptionIsNoticed()
		{
			var timeOfError = DateTime.UtcNow;

			var errorDataIn = ErrorData.FromParts("My message", "My type name", timeOfError, false);

			var transaction = BuildTestTransaction(uri: "http://www.newrelic.com/test?param=value", transactionExceptionDatas: new[] { errorDataIn });

			var errorDataOut = ErrorData.TryGetErrorData(transaction, _configurationService);

			Assert.NotNull(errorDataOut.ErrorTypeName);
			NrAssert.Multiple(
				() => Assert.AreEqual("My type name", errorDataOut.ErrorTypeName),
				() => Assert.AreEqual("My message", errorDataOut.ErrorMessage),
				() => Assert.AreEqual(errorDataIn.NoticedAt, errorDataOut.NoticedAt)
			);
		}

		[Test]
		public void TryGetErrorData_ReturnsFirstException_IfMultipleExceptionsNoticed()
		{
			var errorData1 = ErrorData.FromParts("My message", "My type name", DateTime.UtcNow, false);
			var errorData2 = ErrorData.FromParts("My message2", "My type name2", DateTime.UtcNow, false);
			var transaction = BuildTestTransaction(uri: "http://www.newrelic.com/test?param=value", transactionExceptionDatas: new[] { errorData1, errorData2 });

			var errorDataOut = ErrorData.TryGetErrorData(transaction, _configurationService);

			Assert.NotNull(errorDataOut.ErrorTypeName);
			NrAssert.Multiple(
				() => Assert.AreEqual("My type name", errorDataOut.ErrorTypeName),
				() => Assert.AreEqual("My message", errorDataOut.ErrorMessage),
				() => Assert.AreEqual(errorData1.NoticedAt, errorDataOut.NoticedAt)
			);
		}
		
		[Test]
		public void TryGetErrorData_ReturnsExceptionsBeforeStatusCodes()
		{
			var errorDataIn = ErrorData.FromParts("My message", "My type name", DateTime.UtcNow, false);
			var transaction = BuildTestTransaction(statusCode: 404, uri: "http://www.newrelic.com/test?param=value", transactionExceptionDatas: new[] { errorDataIn });

			var errorDataOut = ErrorData.TryGetErrorData(transaction, _configurationService);

			Assert.NotNull(errorDataOut.ErrorTypeName);

			NrAssert.Multiple(
				() => Assert.AreEqual("My type name", errorDataOut.ErrorTypeName),
				() => Assert.AreEqual("My message", errorDataOut.ErrorMessage),
				() => Assert.AreEqual(errorDataIn.NoticedAt, errorDataOut.NoticedAt)
			);
		}
		
		[Test]
		public void TryGetErrorData_ReturnsNull_IfStatusCodeIsIgnoredByConfig()
		{
			Mock.Arrange(() => _configuration.HttpStatusCodesToIgnore).Returns(new[] { "404" });
			var transaction = BuildTestTransaction(statusCode: 404);

			var errorDataOut = ErrorData.TryGetErrorData(transaction, _configurationService);

			Assert.IsNull(errorDataOut.ErrorTypeName);
		}
		
		[Test]
		public void TryGetErrorData_ReturnsNull_IfAnyExceptionIsIgnored()
		{
			Mock.Arrange(() => _configuration.ExceptionsToIgnore).Returns(new[] { "My type name2" });
			
			var errorData1 = ErrorData.FromParts("My message", "My type name", DateTime.UtcNow, false);
			var errorData2 = ErrorData.FromParts("My message2", "My type name2", DateTime.UtcNow, false);
			var transaction = BuildTestTransaction(transactionExceptionDatas: new[] { errorData1, errorData2 });

			var errorDataOut = ErrorData.TryGetErrorData(transaction, _configurationService);

			Assert.IsNull(errorDataOut.ErrorTypeName);
		}

		[NotNull]
		private static ImmutableTransaction BuildTestTransaction(DateTime startTime, TimeSpan duration, String uri = null, String guid = null, Int32? statusCode = null, Int32? subStatusCode = null, IEnumerable<ErrorData> transactionExceptionDatas = null)
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

			return new ImmutableTransaction(name, segments, metadata, startTime , duration, guid, false, false, false, SqlObfuscator.GetObfuscatingSqlObfuscator());
		}

		[NotNull]
		private static ImmutableTransaction BuildTestTransaction(String uri = null, String guid = null,
			Int32? statusCode = null, Int32? subStatusCode = null, IEnumerable<ErrorData> transactionExceptionDatas = null)
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