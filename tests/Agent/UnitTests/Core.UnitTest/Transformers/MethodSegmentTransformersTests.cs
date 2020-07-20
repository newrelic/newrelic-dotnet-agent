﻿using System;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Configuration;
using NewRelic.Collections;

namespace NewRelic.Agent.Core.Transformers
{
	[TestFixture]
	public class MethodSegmentTransformersTests
	{
		[NotNull]
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
		const String type = "type";
		const String method = "method";
		var segment = GetSegment(type, method);

			//make sure it does not throw
			segment.AddMetricStats(null, _configurationService);


		}

		[Test]
	public void TransformSegment_CreatesCustomSegmentMetrics()
	{
		const String type = "type";
		const String method = "method";
		var segment = GetSegment(type, method, 5);
		segment.ChildFinished(GetSegment("kid", "method", 2));

		var txName = new TransactionMetricName("WebTransaction", "Test", false);
		var txStats = new TransactionMetricStatsCollection(txName);
			segment.AddMetricStats(txStats, _configurationService);


		var scoped = txStats.GetScopedForTesting();
		var unscoped = txStats.GetUnscopedForTesting();

		Assert.AreEqual(1, scoped.Count);
		Assert.AreEqual(1, unscoped.Count);

		const String metricName = "DotNet/type/method";
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
		const String type = "type";
		const String method = "method";
		var segment = GetSegment(type, method); ;

		var txName = new TransactionMetricName("WebTransaction", "Test", false);
		var txStats = new TransactionMetricStatsCollection(txName);
			segment.AddMetricStats(txStats, _configurationService);
			segment.AddMetricStats(txStats, _configurationService);

		var scoped = txStats.GetScopedForTesting();
		var unscoped = txStats.GetUnscopedForTesting();

		Assert.AreEqual(1, scoped.Count);
		Assert.AreEqual(1, unscoped.Count);

		const String metricName = "DotNet/type/method";
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
		const String type = "type";
		const String method = "method";
		var segment = GetSegment(type, method);

		const String type1 = "type1";
		const String method1 = "method1";
		var segment1 = GetSegment(type1, method1);

		var txName = new TransactionMetricName("WebTransaction", "Test", false);
		var txStats = new TransactionMetricStatsCollection(txName);
		segment.AddMetricStats(txStats, _configurationService);

		segment1.AddMetricStats(txStats, _configurationService);

		var scoped = txStats.GetScopedForTesting();
		var unscoped = txStats.GetUnscopedForTesting();

		Assert.AreEqual(2, scoped.Count);
		Assert.AreEqual(2, unscoped.Count);

		const String metricName = "DotNet/type/method";
		Assert.IsTrue(scoped.ContainsKey(metricName));
		Assert.IsTrue(unscoped.ContainsKey(metricName));

		var nameScoped = scoped[metricName];
		var nameUnscoped = unscoped[metricName];

		Assert.AreEqual(1, nameScoped.Value0);
		Assert.AreEqual(1, nameUnscoped.Value0);

		const String metricName1 = "DotNet/type1/method1";
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
			const String type = "type";
			const String method = "method";
			var segment = GetSegment(type, method);

			var transactionTraceName = segment.GetTransactionTraceName();

			Assert.AreEqual("DotNet/type/method", transactionTraceName);
		}

		#endregion GetTransactionTraceName

		[NotNull]
		private static Segment GetSegment([NotNull] String type, [NotNull] String method)
		{
			var timerFactory = Mock.Create<ITimerFactory>();
			var builder = new TypedSegment<MethodSegmentData>(Mock.Create<ITransactionSegmentState>(), new MethodCallData("foo", "bar", 1), new MethodSegmentData(type, method));
			builder.End();
			return builder;
		}

		private static TypedSegment<MethodSegmentData> GetSegment([NotNull] String type, [NotNull] String method, double duration)
		{
			var methodCallData = new MethodCallData("foo", "bar", 1);
			var parameters = (new ConcurrentDictionary<String, Object>());
			return MethodSegmentDataTests.createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(duration), 2, 1, methodCallData, parameters, type, method, false);
		}
	}
}
