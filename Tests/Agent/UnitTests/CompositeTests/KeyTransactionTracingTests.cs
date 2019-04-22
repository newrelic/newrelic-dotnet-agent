using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace CompositeTests
{
	internal class KeyTransactionTracingTests
    {
	    [NotNull] private static CompositeTestAgent _compositeTestAgent;

	    [NotNull] private IAgent _agent;

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
	    public void keytransaction_trace_not_created_when_not_configured()
	    {
			// ARRANGE
			var keyTransactions = new Dictionary<string, double>
			{
				{ "WebTransaction/Action/other", 0.1 }
			};
			_compositeTestAgent.ServerConfiguration.WebTransactionsApdex = keyTransactions;
		    _compositeTestAgent.ServerConfiguration.ApdexT = 10.0;
		    _compositeTestAgent.ServerConfiguration.RpmConfig.TransactionTracerThreshold = 10.0;
			_compositeTestAgent.PushConfiguration();

			// ==== ACT ====
			var tx = _agent.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
			segment.End();
			tx.End();

		    _compositeTestAgent.Harvest();
		    // ==== ACT ====


		    // ASSERT
			Assert.IsEmpty(_compositeTestAgent.TransactionTraces);
	    }

	    [Test]
	    public void keytransaction_trace_not_created_when_configured_and_not_above_apdexT()
	    {
		    // ARRANGE
		    var keyTransactions = new Dictionary<string, double>
		    {
			    { "WebTransaction/Action/name", 10.0 }
		    };
		    _compositeTestAgent.ServerConfiguration.WebTransactionsApdex = keyTransactions;
		    _compositeTestAgent.ServerConfiguration.ApdexT = 10.0;
		    _compositeTestAgent.ServerConfiguration.RpmConfig.TransactionTracerThreshold = 10.0;
		    _compositeTestAgent.PushConfiguration();

			// ==== ACT ====
			var tx = _agent.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
			segment.End();
			tx.End();
		    _compositeTestAgent.Harvest();
			// ==== ACT ====


			// ASSERT
			Assert.IsEmpty(_compositeTestAgent.TransactionTraces);
		}

		[Test]
	    public void keytransaction_trace_created_when_configured_and_above_apdexT()
	    {
		    // ARRANGE
		    var keyTransactions = new Dictionary<string, double>
		    {
			    { "WebTransaction/Action/name", 0.00001 }
		    };
		    _compositeTestAgent.ServerConfiguration.WebTransactionsApdex = keyTransactions;
		    _compositeTestAgent.ServerConfiguration.ApdexT = 10.0;
		    _compositeTestAgent.ServerConfiguration.RpmConfig.TransactionTracerThreshold = 10.0;
		    _compositeTestAgent.PushConfiguration();

			// ==== ACT ====
			var tx = _agent.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
			segment.End();
			tx.End();
			_compositeTestAgent.Harvest();
			// ==== ACT ====


			// ASSERT
			var transactionTrace = _compositeTestAgent.TransactionTraces.First();

			Assert.AreEqual("WebTransaction/Action/name", transactionTrace.TransactionMetricName);
	    }

	    [Test]
	    public void keytransaction_worst_trace_collected()
	    {
		    // ARRANGE
		    var keyTransactions = new Dictionary<string, double>
		    {
			    { "WebTransaction/Action/name", 0.001 },
			    { "WebTransaction/Action/name2", 0.0000001 } // will generate a higher "score"
		};
		    _compositeTestAgent.ServerConfiguration.WebTransactionsApdex = keyTransactions;
		    _compositeTestAgent.ServerConfiguration.ApdexT = 10.0;
		    _compositeTestAgent.ServerConfiguration.RpmConfig.TransactionTracerThreshold = 10.0;
		    _compositeTestAgent.PushConfiguration();

			// ==== ACT ====
			var tx = _agent.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
			segment.End();
			tx.End();

			tx = _agent.CreateWebTransaction(WebTransactionType.Action, "name2");
			segment = _agent.StartTransactionSegmentOrThrow("segmentName2");
			segment.End();
			tx.End();

			_compositeTestAgent.Harvest();
		    // ==== ACT ====


		    // ASSERT
		    var transactionTrace = _compositeTestAgent.TransactionTraces.First();

		    Assert.AreEqual("WebTransaction/Action/name2", transactionTrace.TransactionMetricName);
	    }
	}
}
