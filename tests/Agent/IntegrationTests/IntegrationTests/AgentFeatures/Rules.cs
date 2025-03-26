// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    /// <summary>
    /// Tests around transaction renaming using URL rules and transaction segment terms
    /// NOTE: These tests depend on the application named "RulesWebApi" having some specific rules in place.
    ///		with prefix "WebTransaction/WebAPI" and whitelist terms "Values Sleep UrlRule".
    ///		Can be found at https://[staging|rpm].newrelic.com/accounts/{accountId}/applications/{applicationId}/segment_terms
    /// </summary>
    [NetFrameworkTest]
    public class Rules : NewRelicIntegrationTest<RulesWebApi>
    {
        private readonly RulesWebApi _fixture;

        public Rules(RulesWebApi fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.AddActions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
            });
            _fixture.Initialize();
        }

        // NOTE: this test depends on the application named "RulesWebApi" having a url rule that matches "WebTransaction/Action/.*/UrlRule" and replaces with "WebTransaction/WebAPI/*/UrlRule"
        // Can be found at https://[staging-]one.newrelic.com/nr1-core/metric-normalization-rules/view-rules/{entityGuid}.
        // | Order | Match                             | Replacement                      | Actions | Target      | Terminate? | Active |
        // | ----- | --------------------------------- | -------------------------------- | ------- | ----------- | ---------- | ------ |
        // | 0     | WebTransaction/WebAPI/.\*/UrlRule | WebTransaction/WebAPI/\*/UrlRule | Replace | RulesWebApi | true       | true   |
        [Fact]
        public void Test()
        {
            var expectedTransactionNames = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric {metricName = "WebTransaction/WebAPI/Values/*"},
                new Assertions.ExpectedMetric {metricName = "WebTransaction/WebAPI/Values/Sleep"},
                new Assertions.ExpectedMetric {metricName = "WebTransaction/WebAPI/*/UrlRule"},
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assertions.MetricsExist(expectedTransactionNames, metrics);
        }
    }
}
