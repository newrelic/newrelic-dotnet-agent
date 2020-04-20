using System;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Spans;

namespace NewRelic.Agent.Core.Transformers
{
	[TestFixture]
	public class SimpleSegmentTransformersTests
	{
		private IConfigurationService _configurationService;

		[SetUp]
		public void SetUp()
		{
			_configurationService = Mock.Create<IConfigurationService>();
		}

		#region Transform

		[Test]
		public void TransformSegment_NullStats()
		{
			const string name = "myname";
			var segment = GetSegment(name);

			//make sure it does not throw
			segment.AddMetricStats(null, _configurationService);
		}

		public void TransformSegment_AddParameter()
		{
			const string name = "myname";
			var segment = GetSegment(name);

			//make sure it does not throw
			segment.AddMetricStats(null, _configurationService);
		}

		[Test]
		public void TransformSegment_CreatesSegmentMetrics()
		{
			const string name = "name";
			var segment = GetSegment(name, 5);
			segment.ChildFinished(GetSegment("kid", 2));

			TransactionMetricName txName = new TransactionMetricName("WebTransaction", "Test", false);
			TransactionMetricStatsCollection txStats = new TransactionMetricStatsCollection(txName);
			segment.AddMetricStats(txStats, _configurationService);

			var scoped = txStats.GetScopedForTesting();
			var unscoped = txStats.GetUnscopedForTesting();

			Assert.AreEqual(1, scoped.Count);
			Assert.AreEqual(1, unscoped.Count);

			const string metricName = "DotNet/name";
			Assert.IsTrue(scoped.ContainsKey(metricName));
			Assert.IsTrue(unscoped.ContainsKey(metricName));

			var data = scoped[metricName];
			Assert.AreEqual(1, data.Value0);
			Assert.AreEqual(5, data.Value1);
			Assert.AreEqual(3, data.Value2);
			Assert.AreEqual(5, data.Value3);
			Assert.AreEqual(5, data.Value4);

			data = unscoped[metricName];
			Assert.AreEqual(1, data.Value0);
			Assert.AreEqual(5, data.Value1);
			Assert.AreEqual(3, data.Value2);
			Assert.AreEqual(5, data.Value3);
			Assert.AreEqual(5, data.Value4);
		}

		[Test]
		public void TransformSegment_TwoTransformCallsSame()
		{
			const string name = "name";
			var segment = GetSegment(name);

			var txName = new TransactionMetricName("WebTransaction", "Test", false);
			var txStats = new TransactionMetricStatsCollection(txName);
			segment.AddMetricStats(txStats, _configurationService);
			segment.AddMetricStats(txStats, _configurationService);

			var scoped = txStats.GetScopedForTesting();
			var unscoped = txStats.GetUnscopedForTesting();

			Assert.AreEqual(1, scoped.Count);
			Assert.AreEqual(1, unscoped.Count);

			const string metricName = "DotNet/name";
			Assert.IsTrue(scoped.ContainsKey(metricName));
			Assert.IsTrue(unscoped.ContainsKey(metricName));

			var nameScoped = scoped[metricName];
			var nameUnscoped = unscoped[metricName];

			Assert.AreEqual(2, nameScoped.Value0);
			Assert.AreEqual(2, nameUnscoped.Value0);
		}

		[Test]
		public void TransformSegment_TwoTransformCallsDifferent()
		{
			const string name = "name";
			var segment = GetSegment(name);

			const string name1 = "otherName";
			var segment1 = GetSegment(name1);

			var txName = new TransactionMetricName("WebTransaction", "Test", false);
			var txStats = new TransactionMetricStatsCollection(txName);
			segment.AddMetricStats(txStats, _configurationService);
			segment1.AddMetricStats(txStats, _configurationService);

			var scoped = txStats.GetScopedForTesting();
			var unscoped = txStats.GetUnscopedForTesting();

			Assert.AreEqual(2, scoped.Count);
			Assert.AreEqual(2, unscoped.Count);

			const string metricName = "DotNet/name";
			Assert.IsTrue(scoped.ContainsKey(metricName));
			Assert.IsTrue(unscoped.ContainsKey(metricName));

			var nameScoped = scoped[metricName];
			var nameUnscoped = unscoped[metricName];

			Assert.AreEqual(1, nameScoped.Value0);
			Assert.AreEqual(1, nameUnscoped.Value0);

			const string metricName1 = "DotNet/otherName";
			Assert.IsTrue(scoped.ContainsKey(metricName1));
			Assert.IsTrue(unscoped.ContainsKey(metricName1));

			nameScoped = scoped[metricName1];
			nameUnscoped = unscoped[metricName1];

			Assert.AreEqual(1, nameScoped.Value0);
			Assert.AreEqual(1, nameUnscoped.Value0);
		}

		#endregion Transform

		#region GetTransactionTraceName

		[Test]
		public void GetTransactionTraceName_ReturnsCorrectName()
		{
			const string name = "name";
			var segment = GetSegment(name);

			var transactionTraceName = segment.GetTransactionTraceName();

			Assert.AreEqual("name", transactionTraceName);
		}

		#endregion GetTransactionTraceName

		private static Segment GetSegment(string name)
		{
			var builder = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("foo", "bar", 1), new SpanAttributeValueCollection());
			builder.SetSegmentData(new SimpleSegmentData(name));
			builder.End();
			return builder;
		}

		public static Segment GetSegment(string name, double duration, TimeSpan start = new TimeSpan())
		{
			return new Segment(start, TimeSpan.FromSeconds(duration), GetSegment(name), null);
		}
	}
}
