using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Tracer;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Metrics
{
	[TestFixture]
	public class MetricNamesTests
	{
		[Test]
		public void GetDotNetInvocation()
		{
			var metricName = MetricNames.GetDotNetInvocation("class", "method");
			var sameMetricName = MetricNames.GetDotNetInvocation("class", "method");
			Assert.AreEqual("DotNet/class/method", metricName.ToString());
			Assert.AreEqual(metricName.GetHashCode(), sameMetricName.GetHashCode());
			Assert.AreEqual(metricName, sameMetricName);
		}

		[Test]
		public void GetDatastoreVendorAll()
		{
			Assert.AreEqual("Datastore/MySQL/all", DatastoreVendor.MySQL.GetDatastoreVendorAll().ToString());
		}

		[Test]
		public void GetDatastoreVendorAllWeb()
		{
			Assert.AreEqual("Datastore/MSSQL/allWeb", DatastoreVendor.MSSQL.GetDatastoreVendorAllWeb().ToString());
		}

		[Test]
		public void GetDatastoreVendorAllOther()
		{
			Assert.AreEqual("Datastore/Oracle/allOther", DatastoreVendor.Oracle.GetDatastoreVendorAllOther().ToString());
		}

		[Test]
		public void GetDatastoreOperation()
		{
			Assert.AreEqual("Datastore/operation/MSSQL/select", DatastoreVendor.MSSQL.GetDatastoreOperation("select").ToString());
		}

		[Test]
		public void GetDatastoreStatement()
		{
			Assert.AreEqual("Datastore/statement/MySQL/users/select", MetricNames.GetDatastoreStatement(DatastoreVendor.MySQL, "users", "select").ToString());
		}

		[Test]
		public void GetDatastoreInstance()
		{
			Assert.AreEqual("Datastore/instance/MSSQL/compy64/808", MetricNames.GetDatastoreInstance(DatastoreVendor.MSSQL, "compy64", "808").ToString());
		}

		#region GetTransactionApdex

		[Test]
		public static void GetTransactionApdex_ReturnsExpectedMetricName()
		{
			var transaction = TestTransactions.CreateDefaultTransaction(true, null, null, null, null, null, "foo", "bar");
			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			var transactionNameMaker = new TransactionMetricNameMaker(new MetricNameService());
			var transactionApdex = MetricNames.GetTransactionApdex(transactionNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName));

			var expectedName = "Apdex/foo/bar";

			Assert.AreEqual(expectedName, transactionApdex);
		}

		#endregion

	}
}
