using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Errors.UnitTest
{
	[TestFixture]
	public class ErrorDataTests
	{
		IList<string> _exceptionsToIgnore;
		IList<string> _httpStatusCodesToIgnore;

		[SetUp]
		public void SetUp()
		{
			_exceptionsToIgnore = new List<string>();
			_httpStatusCodesToIgnore = new List<string>();
		}

		[Test]
		public void TryGetErrorData_ReturnsEmpty_IfNoErrors()
		{
			var transaction = BuildTestTransaction(DateTime.UtcNow, TimeSpan.FromSeconds(1));

			var errorData = ErrorData.TryGetErrorData(transaction, Enumerable.Empty<string>(), Enumerable.Empty<string>());

			Assert.Null(errorData.ErrorTypeName);
		}

		[Test]
		public void TryGetErrorData_ReturnsEmpty_IfNoErrorsAndStatusCodeIs200()
		{
			var transaction = BuildTestTransaction(DateTime.UtcNow, TimeSpan.FromSeconds(1), statusCode: 200);

			var errorData = ErrorData.TryGetErrorData(transaction, _exceptionsToIgnore, _httpStatusCodesToIgnore);

			Assert.Null(errorData.ErrorTypeName);
		}

		[Test]
		public void TryGetErrorData_ReturnsErrorData_IfStatusCodeIs404()
		{
			var startTime = DateTime.UtcNow;
			var duration = TimeSpan.FromSeconds(2);
			var responseTime = TimeSpan.FromSeconds(1);

			var transaction = BuildTestTransaction(startTime, duration, responseTime, statusCode: 404, uri: "http://www.newrelic.com/test?param=value");

			var errorData = ErrorData.TryGetErrorData(transaction, _exceptionsToIgnore, _httpStatusCodesToIgnore);

			Assert.NotNull(errorData.ErrorTypeName);
			NrAssert.Multiple(
				() => Assert.AreEqual("404", errorData.ErrorTypeName),
				() => Assert.AreEqual("Not Found", errorData.ErrorMessage),
				() => Assert.AreEqual(startTime + responseTime, errorData.NoticedAt)
				);
		}

		[Test]
		public void TryGetErrorData_ReturnsErrorTrace_IfStatusCodeIs404AndSubstatusCodeIs5()
		{
			var startTime = DateTime.UtcNow;
			var duration = TimeSpan.FromSeconds(4);
			var responseTime = TimeSpan.FromSeconds(2);

			var transaction = BuildTestTransaction(startTime, duration, responseTime, statusCode: 404, subStatusCode: 5, uri: "http://www.newrelic.com/test?param=value");

			var errorData = ErrorData.TryGetErrorData(transaction, _exceptionsToIgnore, _httpStatusCodesToIgnore);

			NrAssert.Multiple(
				() => Assert.AreEqual("404.5", errorData.ErrorTypeName),
				() => Assert.AreEqual("Not Found", errorData.ErrorMessage),
				() => Assert.AreEqual(startTime + responseTime, errorData.NoticedAt)
				);
		}

		[Test]
		public void TryGetErrorData_ReturnsErrorTrace_IfExceptionIsNoticed()
		{
			var timeOfError = DateTime.UtcNow;

			var errorDataIn = ErrorData.FromParts("My message", "My type name", timeOfError, false);

			var transaction = BuildTestTransaction(uri: "http://www.newrelic.com/test?param=value", transactionExceptionDatas: new[] { errorDataIn });

			var errorDataOut = ErrorData.TryGetErrorData(transaction, _exceptionsToIgnore, _httpStatusCodesToIgnore);

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

			var errorDataOut = ErrorData.TryGetErrorData(transaction, _exceptionsToIgnore, _httpStatusCodesToIgnore);

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

			var errorDataOut = ErrorData.TryGetErrorData(transaction, _exceptionsToIgnore, _httpStatusCodesToIgnore);

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
			_httpStatusCodesToIgnore.Add("404");
			var transaction = BuildTestTransaction(statusCode: 404);

			var errorDataOut = ErrorData.TryGetErrorData(transaction, _exceptionsToIgnore, _httpStatusCodesToIgnore);

			Assert.IsNull(errorDataOut.ErrorTypeName);
		}
		
		[Test]
		public void TryGetErrorData_ReturnsNull_IfStatusCodeIsIgnoredByConfig_EvenIfExceptionIsNoticed()
		{
			_httpStatusCodesToIgnore.Add("404");

			var errorDataIn = ErrorData.FromParts("My message", "My type name", DateTime.UtcNow, false);
			var transaction = BuildTestTransaction(statusCode: 404, transactionExceptionDatas: new[] { errorDataIn });
			
			var errorDataOut = ErrorData.TryGetErrorData(transaction, _exceptionsToIgnore, _httpStatusCodesToIgnore);

			Assert.IsNull(errorDataOut.ErrorTypeName);
		}
		
		[Test]
		public void TryGetErrorData_ReturnsNull_IfAnyExceptionIsIgnored()
		{
			_exceptionsToIgnore.Add("My type name2");
			
			var errorData1 = ErrorData.FromParts("My message", "My type name", DateTime.UtcNow, false);
			var errorData2 = ErrorData.FromParts("My message2", "My type name2", DateTime.UtcNow, false);
			var transaction = BuildTestTransaction(transactionExceptionDatas: new[] { errorData1, errorData2 });

			var errorDataOut = ErrorData.TryGetErrorData(transaction, _exceptionsToIgnore, _httpStatusCodesToIgnore);

			Assert.IsNull(errorDataOut.ErrorTypeName);
		}
		
		[Test]
		public void TryGetErrorTrace_ReturnsNull_IfAnyExceptionIsIgnored_EvenIfStatusCodeIs404()
		{
			_exceptionsToIgnore.Add("My type name2");

			var errorData1 = ErrorData.FromParts("My message", "My type name", DateTime.UtcNow, false);
			var errorData2 = ErrorData.FromParts("My message2", "My type name2", DateTime.UtcNow, false);
			var transaction = BuildTestTransaction(statusCode: 404, transactionExceptionDatas: new[] { errorData1, errorData2 });

			var errorDataOut = ErrorData.TryGetErrorData(transaction, _exceptionsToIgnore, _httpStatusCodesToIgnore);

			Assert.IsNull(errorDataOut.ErrorTypeName);
		}

		private static ImmutableTransaction BuildTestTransaction(DateTime startTime, TimeSpan duration, TimeSpan? responseTime = null, string uri = null, string guid = null, int? statusCode = null, int? subStatusCode = null, IEnumerable<ErrorData> transactionExceptionDatas = null)
		{
			var transactionMetadata = new TransactionMetadata();
			if (uri != null)
				transactionMetadata.SetUri(uri);
			if (statusCode != null)
				transactionMetadata.SetHttpResponseStatusCode(statusCode.Value, subStatusCode);
			if (transactionExceptionDatas != null)
				transactionExceptionDatas.ForEach(data => transactionMetadata.AddExceptionData(data));

			var name = TransactionName.ForWebTransaction("foo", "bar");
			var segments = Enumerable.Empty<Segment>();
			var metadata = transactionMetadata.ConvertToImmutableMetadata();
			guid = guid ?? Guid.NewGuid().ToString();
			responseTime = responseTime ?? duration;

			return new ImmutableTransaction(name, segments, metadata, startTime , duration, responseTime, guid, false, false, false);
		}

		private static ImmutableTransaction BuildTestTransaction(string uri = null, string guid = null,
			int? statusCode = null, int? subStatusCode = null, IEnumerable<ErrorData> transactionExceptionDatas = null)
		{
			var transactionMetadata = new TransactionMetadata();
			if (uri != null)
				transactionMetadata.SetUri(uri);
			if (statusCode != null)
				transactionMetadata.SetHttpResponseStatusCode(statusCode.Value, subStatusCode);
			if (transactionExceptionDatas != null)
				transactionExceptionDatas.ForEach(data => transactionMetadata.AddExceptionData(data));

			var name = TransactionName.ForWebTransaction("foo", "bar");
			var segments = Enumerable.Empty<Segment>();
			var metadata = transactionMetadata.ConvertToImmutableMetadata();
			guid = guid ?? Guid.NewGuid().ToString();

			return new ImmutableTransaction(name, segments, metadata, DateTime.UtcNow, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), guid, false, false, false);
		}

	}
}
