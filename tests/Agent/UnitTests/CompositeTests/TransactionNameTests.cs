using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace CompositeTests
{
    [TestFixture]
    public class TransactionNameTests
    {
        [NotNull]
        private static CompositeTestAgent _compositeTestAgent;

        [NotNull]
        private IAgentWrapperApi _agentWrapperApi;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        #region Segment metrics and trace names

        [Test]
        public void SetWebTransactionName_UpdatesTransactionNameCorrectly()
        {
            using (var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
            {
                transaction.SetWebTransactionName(WebTransactionType.ASP, "foo", 4);
                var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("simpleName");
                segment.End();
            }

            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
                new ExpectedMetric {Name = "WebTransaction/ASP/foo"}
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
                () => Assert.AreEqual("WebTransaction/ASP/foo", transactionTrace.TransactionMetricName)
                );
        }

        [Test]
        public void SetWebTransactionNameFromPath_UpdatesTransactionNameCorrectly()
        {
            using (var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
            {
                transaction.SetWebTransactionNameFromPath(WebTransactionType.ASP, "foo");
                _agentWrapperApi.StartTransactionSegmentOrThrow("simpleName").End();
            }

            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
                new ExpectedMetric {Name = "WebTransaction/Uri/foo"}
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
                () => Assert.AreEqual("WebTransaction/Uri/foo", transactionTrace.TransactionMetricName)
                );
        }

        [Test]
        public void SetOtherTransactionName_UpdatesTransactionNameCorrectly()
        {
            using (var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
            {
                transaction.SetOtherTransactionName("cat", "foo", 4);
                var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("simpleName");
                segment.End();
            }

            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
                new ExpectedMetric {Name = "OtherTransaction/cat/foo"}
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
                () => Assert.AreEqual("OtherTransaction/cat/foo", transactionTrace.TransactionMetricName)
                );
        }

        [Test]
        public void SetMessageBrokerTransactionName_UpdatesTransactionNameCorrectly()
        {
            using (var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
            {
                transaction.SetMessageBrokerTransactionName(MessageBrokerDestinationType.Queue, "vendor", "dest", 4);
                var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("simpleName");
                segment.End();
            }

            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
                new ExpectedMetric {Name = "OtherTransaction/Message/vendor/Queue/Named/dest"}
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
                () => Assert.AreEqual("OtherTransaction/Message/vendor/Queue/Named/dest", transactionTrace.TransactionMetricName)
                );
        }

        [Test]
        public void SetCustomTransactionName_UpdatesTransactionNameCorrectly_IfWebTransaction()
        {
            using (var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
            {
                transaction.SetCustomTransactionName("foo", 4);
                var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("simpleName");
                segment.End();
            }

            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
                new ExpectedMetric {Name = "WebTransaction/Custom/foo" }
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
                () => Assert.AreEqual("WebTransaction/Custom/foo", transactionTrace.TransactionMetricName)
                );
        }

        [Test]
        public void SetCustomTransactionName_UpdatesTransactionNameCorrectly_IfNonWebTransaction()
        {
            using (var transaction = _agentWrapperApi.CreateOtherTransaction("cat", "name"))
            {
                transaction.SetCustomTransactionName("foo", 4);
                var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("simpleName");
                segment.End();
            }
            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
                new ExpectedMetric {Name = "OtherTransaction/Custom/foo"}
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
                () => Assert.AreEqual("OtherTransaction/Custom/foo", transactionTrace.TransactionMetricName)
                );
        }

        [Test]
        public void SetCustomTransactionName_UpdatesTransactionNameCorrectly_IfNameIsPrefixedWithCustom()
        {
            using (var transaction = _agentWrapperApi.CreateOtherTransaction("cat", "name"))
            {
                transaction.SetCustomTransactionName("Custom/foo", 4);
                var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("simpleName");
                segment.End();
            }

            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
				// The agent should de-duplicate the "Custom/" prefix that was passed in
				new ExpectedMetric {Name = "OtherTransaction/Custom/foo"}
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
                () => Assert.AreEqual("OtherTransaction/Custom/foo", transactionTrace.TransactionMetricName)
                );
        }

        #endregion
    }
}
