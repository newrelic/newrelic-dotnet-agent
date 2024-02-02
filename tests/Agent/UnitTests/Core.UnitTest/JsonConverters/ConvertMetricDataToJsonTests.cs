// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Labels;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.JsonConverters
{
    [TestFixture]
    public class ConvertMetricDataToJsonTests
    {
        private readonly IMetricNameService _metricNameService = Mock.Create<IMetricNameService>();
        private ConnectionHandler _connectionHandler;

        private MetricWireModelCollection _wellformedMetricData;
        private string _wellformedJson = "[\"440491846668652\",1450462672.0,1450462710.0,[[{\"name\":\"DotNet/name\",\"scope\":\"WebTransaction/DotNet/name\"},[1,3.0,2.0,3.0,3.0,9.0]],[{\"name\":\"Custom/name\"},[1,4.0,3.0,4.0,4.0,16.0]]]]";


        [SetUp]
        public void Setup()
        {
            Mock.Arrange(() => _metricNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(name => name);
            _connectionHandler = new ConnectionHandler(new JsonSerializer(), Mock.Create<ICollectorWireFactory>(), Mock.Create<IProcessStatic>(), Mock.Create<IDnsStatic>(),
                Mock.Create<ILabelsService>(), Mock.Create<Environment>(), Mock.Create<ISystemInfo>(), Mock.Create<IAgentHealthReporter>(), Mock.Create<IEnvironment>());

            var validScopedMetric = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "WebTransaction/DotNet/name",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3.0), TimeSpan.FromSeconds(2.0)));

            var validUnscopedMetric = MetricWireModel.BuildMetric(_metricNameService, "Custom/name", string.Empty,
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(4.0), TimeSpan.FromSeconds(3.0)));

            var validMetricWireModels = new List<MetricWireModel> { validScopedMetric, validUnscopedMetric };
            _wellformedMetricData = new MetricWireModelCollection("440491846668652", 1450462672.0, 1450462710.0, validMetricWireModels);
        }

        [TearDown]
        [OneTimeTearDown]
        public void TearDown()
        {
            _metricNameService.Dispose();
            _connectionHandler.Dispose();
        }
        [Test]
        public void Serialize_NoErrors()
        {
            Assert.DoesNotThrow(() => _connectionHandler.SendDataRequest<object>("metric_data", _wellformedMetricData));
        }

        [Test]
        public void Serialize_MatchesExpectedOutput()
        {
            var model = new MetricWireModelCollection[] { _wellformedMetricData };

            var serializedMetrics = Newtonsoft.Json.JsonConvert.SerializeObject(model);
            Assert.That(serializedMetrics, Is.EqualTo(_wellformedJson));
        }
    }
}
