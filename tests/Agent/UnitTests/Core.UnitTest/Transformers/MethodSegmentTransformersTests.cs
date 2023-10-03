// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Spans;

namespace NewRelic.Agent.Core.Transformers
{
    [TestFixture]
    public class MethodSegmentTransformersTests
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
            const string type = "type";
            const string method = "method";
            var segment = GetSegment(type, method);

            //make sure it does not throw
            segment.AddMetricStats(null, _configurationService);


        }

        [Test]
        public void TransformSegment_CreatesCustomSegmentMetrics()
        {
            const string type = "type";
            const string method = "method";
            var segment = GetSegment(type, method, 5);
            segment.ChildFinished(GetSegment("kid", "method", 2));

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);


            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            Assert.AreEqual(1, scoped.Count);
            Assert.AreEqual(1, unscoped.Count);

            const string metricName = "DotNet/type/method";
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
            const string type = "type";
            const string method = "method";
            var segment = GetSegment(type, method); ;

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);
            segment.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            Assert.AreEqual(1, scoped.Count);
            Assert.AreEqual(1, unscoped.Count);

            const string metricName = "DotNet/type/method";
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
            const string type = "type";
            const string method = "method";
            var segment = GetSegment(type, method);

            const string type1 = "type1";
            const string method1 = "method1";
            var segment1 = GetSegment(type1, method1);

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);

            segment1.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            Assert.AreEqual(2, scoped.Count);
            Assert.AreEqual(2, unscoped.Count);

            const string metricName = "DotNet/type/method";
            Assert.IsTrue(scoped.ContainsKey(metricName));
            Assert.IsTrue(unscoped.ContainsKey(metricName));

            var nameScoped = scoped[metricName];
            var nameUnscoped = unscoped[metricName];

            Assert.AreEqual(1, nameScoped.Value0);
            Assert.AreEqual(1, nameUnscoped.Value0);

            const string metricName1 = "DotNet/type1/method1";
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
            const string type = "type";
            const string method = "method";
            var segment = GetSegment(type, method);

            var transactionTraceName = segment.GetTransactionTraceName();

            Assert.AreEqual("DotNet/type/method", transactionTraceName);
        }

        #endregion GetTransactionTraceName

        private static Segment GetSegment(string type, string method)
        {
            var builder = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("foo", "bar", 1));
            builder.SetSegmentData(new MethodSegmentData(type, method));
            builder.End();
            return builder;
        }

        private static Segment GetSegment(string type, string method, double duration)
        {
            var methodCallData = new MethodCallData("foo", "bar", 1);
            var parameters = (new Dictionary<string, object>());
            return MethodSegmentDataTests.createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(duration), 2, 1, methodCallData, parameters, type, method, false);
        }
    }
}
