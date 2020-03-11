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

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	[TestFixture]
	public class SqlTraceMakerTests
	{
		private IDatabaseService _databaseService;

		private IConfigurationService _configurationService;

		private SqlTraceMaker _sqlTraceMaker;

		private IAttributeService _attributeService;

		private IErrorService _errorService;

		[SetUp]
		public void SetUp()
		{
			_databaseService = Mock.Create<IDatabaseService>();
			Mock.Arrange(() => _databaseService.GetObfuscatedSql(Arg.AnyString, Arg.IsAny<DatastoreVendor>())).Returns((string sql) => sql);
			_configurationService = Mock.Create<IConfigurationService>();
			Mock.Arrange(() => _configurationService.Configuration.InstanceReportingEnabled).Returns(true);
			Mock.Arrange(() => _configurationService.Configuration.DatabaseNameReportingEnabled).Returns(true);
			_attributeService = Mock.Create<IAttributeService>();
			_sqlTraceMaker = new SqlTraceMaker(_configurationService, _attributeService, _databaseService);
			_errorService = new ErrorService(_configurationService);
		}

		[Test]
		public void TryGetSqlTrace_ReturnsTrace()
		{
			Mock.Arrange(() => _attributeService.AllowRequestUri(AttributeDestinations.SqlTrace)).Returns(true);

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
			Mock.Arrange(() => _attributeService.AllowRequestUri(AttributeDestinations.SqlTrace)).Returns(true);

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
			Mock.Arrange(() => _attributeService.AllowRequestUri(AttributeDestinations.SqlTrace)).Returns(true);

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
			Mock.Arrange(() => _attributeService.AllowRequestUri(AttributeDestinations.SqlTrace)).Returns(false);

			var uri = "sqlTrace/Uri";
			var commandText = "Select * from Table1";
			var duration = TimeSpan.FromMilliseconds(500);
			var transaction = BuildTestTransaction(uri);
			var transactionMetricName = new TransactionMetricName("WebTransaction", "Name");
			var datastoreSegment = BuildSegment(DatastoreVendor.MSSQL, "Table1", commandText, new TimeSpan(), duration, null, null, null, "myhost", "myport", "mydatabase");

			var sqlTrace = _sqlTraceMaker.TryGetSqlTrace(transaction, transactionMetricName, datastoreSegment);
			Assert.IsNotNull(sqlTrace);
			Assert.AreEqual("<unknown>", sqlTrace.Uri);
		}

		private ImmutableTransaction BuildTestTransaction(string uri = null, string guid = null, int? statusCode = null, int? subStatusCode = null, IEnumerable<ErrorData> transactionExceptionDatas = null)
		{
			var txMetadata = new TransactionMetadata();
			if (uri != null)
				txMetadata.SetUri(uri);
			if (statusCode != null)
				txMetadata.SetHttpResponseStatusCode(statusCode.Value, subStatusCode, _errorService);
			if (transactionExceptionDatas != null)
				transactionExceptionDatas.ForEach(data => txMetadata.AddExceptionData(data));

			var name = TransactionName.ForWebTransaction("foo", "bar");
			var segments = Enumerable.Empty<Segment>();
			var immutableMetadata = txMetadata.ConvertToImmutableMetadata();
			guid = guid ?? Guid.NewGuid().ToString();

			return new ImmutableTransaction(name, segments, immutableMetadata, DateTime.UtcNow, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), guid, false, false, false, 0.5f, false, string.Empty, null);
		}

		private Segment BuildSegment(DatastoreVendor vendor, string model, string commandText, TimeSpan startTime = new TimeSpan(), TimeSpan? duration = null, string name = "", MethodCallData methodCallData = null, IEnumerable<KeyValuePair<string, object>> parameters = null, string host = null, string portPathOrId = null, string databaseName = null)
		{
			var data = new DatastoreSegmentData(_databaseService, new ParsedSqlStatement(vendor, model, null), commandText,
				new ConnectionInfo(host, portPathOrId, databaseName));
			methodCallData = methodCallData ?? new MethodCallData("typeName", "methodName", 1);

			var segment = new Segment(Mock.Create<ITransactionSegmentState>(), methodCallData);
			segment.SetSegmentData(data);

			return new Segment(startTime, duration, segment, parameters);
		}
	}
}
