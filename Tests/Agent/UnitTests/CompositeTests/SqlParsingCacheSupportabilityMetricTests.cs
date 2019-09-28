using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using System.Collections.Generic;
using System.Data;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Api;

namespace CompositeTests
{
	[TestFixture]
	public class SqlParsingCacheSupportabilityMetricTests
	{
		private const string SupportabilityMetricPrefix = "Supportability/SqlParsingCache";

		private static CompositeTestAgent _compositeTestAgent;
		private IAgent _agent;
		private IDatabaseStatementParser _databaseStatementParser;

		[SetUp]
		public void SetUp()
		{
			_compositeTestAgent = new CompositeTestAgent();
			_agent = _compositeTestAgent.GetAgent();
			_databaseStatementParser = _compositeTestAgent.GetDatabaseStatementParser();
		}

		[TearDown]
		public static void TearDown()
		{
			_compositeTestAgent.Dispose();
		}

		[Test]
		public void SqlParsingCacheMetricsAreGenerated()
		{
			_databaseStatementParser.ResetCaches();

			_agent.CreateWebTransaction(WebTransactionType.Action, "name");

			_agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table1").End();
			_agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table1").End();
			_agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table1").End();
			_agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table2").End();
			_agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table2").End();

			_agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.IBMDB2, CommandType.Text, "SELECT * FROM Table1").End();
			_agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.IBMDB2, CommandType.Text, "SELECT * FROM Table1").End();


			_compositeTestAgent.Harvest();

			const int defaultCapactity = 1000;

			// ASSERT
			var expectedMetrics = new List<ExpectedMetric>
			{
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/Capacity", CallCount = 1, Total = defaultCapactity},

				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/MSSQL/Hits", CallCount = 3},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/MSSQL/Misses", CallCount = 2},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/MSSQL/Ejections", CallCount = 0},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/MSSQL/Size", CallCount = 1, Total = 2},

				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/IBMDB2/Hits", CallCount = 1},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/IBMDB2/Misses", CallCount = 1},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/IBMDB2/Ejections", CallCount = 0},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/IBMDB2/Size", CallCount = 1, Total = 1}
			};

			var unexpectedMetrics = new List<ExpectedMetric>
			{
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/MongoDB/Hits"},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/MongoDB/Misses"},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/MongoDB/Ejections"},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/MongoDB/Size"}
			};

			MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);
			MetricAssertions.MetricsDoNotExist(unexpectedMetrics, _compositeTestAgent.Metrics);
		}

		[Test]
		public void SqlParsingCacheMetricsAreResetBetweenHarvests()
		{
			_databaseStatementParser.ResetCaches();

			_agent.CreateWebTransaction(WebTransactionType.Action, "name");

			_agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table1").End();
			_agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table1").End();
			_agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table1").End();
			_agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table2").End();
			_agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table2").End();

			_agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.IBMDB2, CommandType.Text, "SELECT * FROM Table1").End();
			_agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.IBMDB2, CommandType.Text, "SELECT * FROM Table1").End();

			const int defaultCapactity = 1000;

			// ASSERT
			var expectedMetrics = new List<ExpectedMetric>
			{
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/Capacity", CallCount = 1, Total = defaultCapactity},

				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/MSSQL/Hits", CallCount = 3},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/MSSQL/Misses", CallCount = 2},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/MSSQL/Ejections", CallCount = 0},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/MSSQL/Size", CallCount = 1, Total = 2},

				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/IBMDB2/Hits", CallCount = 1},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/IBMDB2/Misses", CallCount = 1},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/IBMDB2/Ejections", CallCount = 0},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/IBMDB2/Size", CallCount = 1, Total = 1}
			};

			_compositeTestAgent.Harvest();

			MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);

			_agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table1").End();
			_agent.StartDatastoreRequestSegmentOrThrow(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM Table1").End();

			expectedMetrics = new List<ExpectedMetric>
			{
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/Capacity", CallCount = 1, Total = defaultCapactity},

				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/MSSQL/Hits", CallCount = 2},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/MSSQL/Misses", CallCount = 0},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/MSSQL/Ejections", CallCount = 0},
				new ExpectedCountMetric {Name =  SupportabilityMetricPrefix + "/MSSQL/Size", CallCount = 1, Total = 2}
			};

			_compositeTestAgent.ResetHarvestData();
			_compositeTestAgent.Harvest();

			MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);



		}

	}
}
