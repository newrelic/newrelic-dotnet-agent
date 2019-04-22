using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Metric;
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

		[Test]
		public static void MetricNamesTest_DistributedTracing()
		{
			Assert.That(MetricNames.SupportabilityDistributedTraceAcceptPayloadSuccess, Is.EqualTo("Supportability/DistributedTrace/AcceptPayload/Success"));
			Assert.That(MetricNames.SupportabilityDistributedTraceAcceptPayloadException, Is.EqualTo("Supportability/DistributedTrace/AcceptPayload/Exception"));
			Assert.That(MetricNames.SupportabilityDistributedTraceAcceptPayloadParseException, Is.EqualTo("Supportability/DistributedTrace/AcceptPayload/ParseException"));
			Assert.That(MetricNames.SupportabilityDistributedTraceAcceptPayloadIgnoredCreateBeforeAccept, Is.EqualTo("Supportability/DistributedTrace/AcceptPayload/Ignored/CreateBeforeAccept"));
			Assert.That(MetricNames.SupportabilityDistributedTraceAcceptPayloadIgnoredMultiple, Is.EqualTo("Supportability/DistributedTrace/AcceptPayload/Ignored/Multiple"));
			Assert.That(MetricNames.SupportabilityDistributedTraceAcceptPayloadIgnoredMajorVersion, Is.EqualTo("Supportability/DistributedTrace/AcceptPayload/Ignored/MajorVersion"));
			Assert.That(MetricNames.SupportabilityDistributedTraceAcceptPayloadIgnoredNull, Is.EqualTo("Supportability/DistributedTrace/AcceptPayload/Ignored/Null"));
			Assert.That(MetricNames.SupportabilityDistributedTraceAcceptPayloadIgnoredUntrustedAccount, Is.EqualTo("Supportability/DistributedTrace/AcceptPayload/Ignored/UntrustedAccount"));
			Assert.That(MetricNames.SupportabilityDistributedTraceCreatePayloadSuccess, Is.EqualTo("Supportability/DistributedTrace/CreatePayload/Success"));
			Assert.That(MetricNames.SupportabilityDistributedTraceCreatePayloadException, Is.EqualTo("Supportability/DistributedTrace/CreatePayload/Exception"));
		}

		[Test]
		public static void MetricNamesTest_AgentFeatureApiVersion()
		{
			Assert.That(MetricNames.GetSupportabilityAgentApi("method"), Is.EqualTo("Supportability/ApiInvocation/method"));
			Assert.That(MetricNames.GetSupportabilityFeatureEnabled("feature"), Is.EqualTo("Supportability/FeatureEnabled/feature"));
			Assert.That(MetricNames.GetSupportabilityAgentVersion("version"), Is.EqualTo("Supportability/AgentVersion/version"));
			Assert.That(MetricNames.GetSupportabilityAgentVersionByHost("host", "version"), Is.EqualTo("Supportability/AgentVersion/host/version"));
			Assert.That(MetricNames.GetSupportabilityLinuxOs(), Is.EqualTo("Supportability/OS/Linux"));
		}

		[Test]
		public static void MetricNamesTest_AgentHealthEvent()
		{
			Assert.That(MetricNames.GetSupportabilityAgentHealthEvent(AgentHealthEvent.TransactionGarbageCollected, null),
				Is.EqualTo("Supportability/TransactionGarbageCollected"));
			Assert.That(MetricNames.GetSupportabilityAgentHealthEvent(AgentHealthEvent.TransactionGarbageCollected, "additional"),
				Is.EqualTo("Supportability/TransactionGarbageCollected/additional"));
			Assert.That(MetricNames.GetSupportabilityAgentHealthEvent(AgentHealthEvent.WrapperShutdown, null),
				Is.EqualTo("Supportability/WrapperShutdown"));
			Assert.That(MetricNames.GetSupportabilityAgentHealthEvent(AgentHealthEvent.WrapperShutdown, "additional"),
				Is.EqualTo("Supportability/WrapperShutdown/additional"));
		}

		[Test]
		public static void MetricNamesTest_Errors()
		{
			Assert.That(MetricNames.SupportabilityErrorTracesSent, 
				Is.EqualTo("Supportability/Errors/TotalErrorsSent"));
			Assert.That(MetricNames.SupportabilityErrorTracesCollected,
				Is.EqualTo("Supportability/Errors/TotalErrorsCollected"));
			Assert.That(MetricNames.SupportabilityErrorTracesRecollected,
				Is.EqualTo("Supportability/Errors/TotalErrorsRecollected"));
		}

		[Test]
		public static void MetricNamesTest_Utilization()
		{
			Assert.That(MetricNames.GetSupportabilityBootIdError(), Is.EqualTo("Supportability/utilization/boot_id/error"));
			Assert.That(MetricNames.GetSupportabilityAwsUsabilityError(), Is.EqualTo("Supportability/utilization/aws/error"));
			Assert.That(MetricNames.GetSupportabilityAzureUsabilityError(),Is.EqualTo("Supportability/utilization/azure/error"));
			Assert.That(MetricNames.GetSupportabilityGcpUsabilityError(), Is.EqualTo("Supportability/utilization/gcp/error"));
			Assert.That(MetricNames.GetSupportabilityPcfUsabilityError(), Is.EqualTo("Supportability/utilization/pcf/error"));

		}

		[Test]
		public static void MetricNamesTest_SqlTraces()
		{
			Assert.That(MetricNames.SupportabilitySqlTracesSent, Is.EqualTo("Supportability/SqlTraces/TotalSqlTracesSent"));
			Assert.That(MetricNames.SupportabilitySqlTracesCollected.ToString(), Is.EqualTo("Supportability/SqlTraces/TotalSqlTracesCollected"));
			Assert.That(MetricNames.SupportabilitySqlTracesRecollected, Is.EqualTo("Supportability/SqlTraces/TotalSqlTracesRecollected"));
		}

		[Test]
		public static void MetricNamesTest_Events()
		{

			Assert.That(MetricNames.SupportabilityErrorEventsSent, Is.EqualTo("Supportability/Events/TransactionError/Sent"));
			Assert.That(MetricNames.SupportabilityErrorEventsSeen, Is.EqualTo("Supportability/Events/TransactionError/Seen"));
			Assert.That(MetricNames.SupportabilityCustomEventsSent, Is.EqualTo("Supportability/Events/Customer/Sent"));
			Assert.That(MetricNames.SupportabilityCustomEventsSeen, Is.EqualTo("Supportability/Events/Customer/Seen"));

			Assert.That(MetricNames.SupportabilityCustomEventsCollected,
				Is.EqualTo("Supportability/Events/Customer/TotalEventsCollected"));
			Assert.That(MetricNames.SupportabilityCustomEventsRecollected,
				Is.EqualTo("Supportability/Events/Customer/TotalEventsRecollected"));
			Assert.That(MetricNames.SupportabilityCustomEventsReservoirResize,
				Is.EqualTo("Supportability/Events/Customer/TryResizeReservoir"));
		}

		[Test]
		public static void MetricNamesTest_AnalyticEvents()
		{
			Assert.That(MetricNames.SupportabilityTransactionEventsSent,
				Is.EqualTo("Supportability/AnalyticsEvents/TotalEventsSent"));
			Assert.That(MetricNames.SupportabilityTransactionEventsSeen,
				Is.EqualTo("Supportability/AnalyticsEvents/TotalEventsSeen"));
			Assert.That(MetricNames.SupportabilityTransactionEventsCollected,
				Is.EqualTo("Supportability/AnalyticsEvents/TotalEventsCollected"));
			Assert.That(MetricNames.SupportabilityTransactionEventsRecollected,
				Is.EqualTo("Supportability/AnalyticsEvents/TotalEventsRecollected"));
			Assert.That(MetricNames.SupportabilityTransactionEventsReservoirResize,
				Is.EqualTo("Supportability/AnalyticsEvents/TryResizeReservoir"));
		}

		[Test]
		public static void MetricNamesTest_MetricHarvest()
		{
			Assert.That(MetricNames.SupportabilityMetricHarvestTransmit, Is.EqualTo("Supportability/MetricHarvest/transmit"));
		}

		[Test]
		public static void MetricNamesTest_MiscellaneousSupportability()
		{
			Assert.That(MetricNames.SupportabilityRumHeaderRendered, Is.EqualTo("Supportability/RUM/Header"));
			Assert.That(MetricNames.SupportabilityRumFooterRendered, Is.EqualTo("Supportability/RUM/Footer"));
			Assert.That(MetricNames.SupportabilityHtmlPageRendered, Is.EqualTo("Supportability/RUM/HtmlPage"));

			Assert.That(MetricNames.SupportabilityThreadProfilingSampleCount, Is.EqualTo("Supportability/ThreadProfiling/SampleCount"));
		}
	}
}
