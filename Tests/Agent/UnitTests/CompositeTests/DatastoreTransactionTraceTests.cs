using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace CompositeTests
{
	[TestFixture]
	public class DatastoreTransactionTraceTests
	{
		private static CompositeTestAgent _compositeTestAgent;

		private IAgent _agent;

		[SetUp]
		public void SetUp()
		{
			_compositeTestAgent = new CompositeTestAgent();
			_agent = _compositeTestAgent.GetAgent();
		}

		[TearDown]
		public static void TearDown()
		{
			_compositeTestAgent.Dispose();
		}

		[Test]
		public void TransactionTrace_HasSqlParameter()
		{
			_compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0;
			_compositeTestAgent.PushConfiguration();

			var tx = _agent.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1");
			segment.End();
			tx.End();

			_compositeTestAgent.Harvest();

			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

			Assert.AreEqual("SELECT * FROM Table1", parameters["sql"]);
		}

		[Test]
		public void TransactionTrace_HasQueryParameters()
		{
			_compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0;
			_compositeTestAgent.LocalConfiguration.transactionTracer.recordSql = configurationTransactionTracerRecordSql.raw;
			_compositeTestAgent.LocalConfiguration.datastoreTracer.queryParameters.enabled = true;
			_compositeTestAgent.PushConfiguration();

			var tx = _agent.CreateWebTransaction(WebTransactionType.Action, "name");
			var queryParameters = new Dictionary<string, IConvertible>
			{
				{"myKey1", "myValue1"},
				{"myKey2", "myValue2"}
			};
			var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1", queryParameters: queryParameters);
			segment.End();
			tx.End();

			_compositeTestAgent.Harvest();

			var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
			Assert.IsNotNull(transactionTrace);

			var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

			Assert.IsTrue(parameters.ContainsKey("query_parameters"));
			CollectionAssert.AreEquivalent((Dictionary<string, IConvertible>)parameters["query_parameters"], new Dictionary<string, IConvertible>
			{
				{"myKey1", "myValue1"},
				{"myKey2", "myValue2"}
			});
		}

		[Test]
		public void TransactionTrace_HasNoQueryParameters()
		{
			_compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0;
			_compositeTestAgent.LocalConfiguration.transactionTracer.recordSql = configurationTransactionTracerRecordSql.raw;
			_compositeTestAgent.LocalConfiguration.datastoreTracer.queryParameters.enabled = false;
			_compositeTestAgent.PushConfiguration();

			var tx = _agent.CreateWebTransaction(WebTransactionType.Action, "name");
			var queryParameters = new Dictionary<string, IConvertible>
			{
				{"myKey", "myValue"}
			};
			var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1", queryParameters: queryParameters);
			segment.End();
			tx.End();

			_compositeTestAgent.Harvest();

			var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
			Assert.IsNotNull(transactionTrace);

			var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

			Assert.IsFalse(parameters.ContainsKey("query_parameters"));
		}

		[Test]
		public void TransactionTrace_NoQueryParameterInput_HasNoQueryParameters()
		{
			_compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0;
			_compositeTestAgent.LocalConfiguration.transactionTracer.recordSql = configurationTransactionTracerRecordSql.raw;
			_compositeTestAgent.LocalConfiguration.datastoreTracer.queryParameters.enabled = true;
			_compositeTestAgent.PushConfiguration();

			var tx = _agent.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1");
			segment.End();
			tx.End();

			_compositeTestAgent.Harvest();

			var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
			Assert.IsNotNull(transactionTrace);

			var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

			Assert.IsFalse(parameters.ContainsKey("query_parameters"));
		}
	}
}
