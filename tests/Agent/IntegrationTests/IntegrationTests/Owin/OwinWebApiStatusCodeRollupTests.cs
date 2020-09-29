// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;

namespace NewRelic.Agent.IntegrationTests.Owin
{
    [NetFrameworkTest]
    public class OwinWebApiStatusCodeRollupTests : IClassFixture<RemoteServiceFixtures.OwinWebApiFixture>
    {
        private readonly RemoteServiceFixtures.OwinWebApiFixture _fixture;

        private readonly List<string> bogusPaths = new List<string> { "no/such/path", "foo/bar/baz", "fizz/buzz", "one/two/red/blue" };

        // The base test class runs tests for Owin 2; the derived classes test Owin 3 and 4
        public OwinWebApiStatusCodeRollupTests(RemoteServiceFixtures.OwinWebApiFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "requestParameters" }, "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    foreach (var bogusPath in bogusPaths)
                    {
                        _fixture.GetBogusPath(bogusPath);
                    }

                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedCallCount = bogusPaths.Count;

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric {metricName = @"DotNet/Owin Middleware Pipeline", callCount = expectedCallCount},
                new Assertions.ExpectedMetric {metricName = @"WebTransaction", callCount = expectedCallCount},
                new Assertions.ExpectedMetric {metricName = @"WebTransaction/StatusCode/404", callCount = expectedCallCount},
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>();
            foreach (var bogusPath in bogusPaths)
            {
                unexpectedMetrics.Add(new Assertions.ExpectedMetric { metricName = $@"WebTransaction/Custom/{bogusPath}", callCount = 1 });
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics)
            );

        }
    }

    public class Owin3WebApiStatusCodeRollupTests : OwinWebApiTests, IClassFixture<RemoteServiceFixtures.Owin3WebApiFixture>
    {
        public Owin3WebApiStatusCodeRollupTests(RemoteServiceFixtures.Owin3WebApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
    public class Owin4WebApiStatusCodeRollupTests : OwinWebApiTests, IClassFixture<RemoteServiceFixtures.Owin4WebApiFixture>
    {
        public Owin4WebApiStatusCodeRollupTests(RemoteServiceFixtures.Owin4WebApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
