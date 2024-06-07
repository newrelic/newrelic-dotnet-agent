// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Linq;
using System.Net.Http.Headers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CatInbound
{
    [NetFrameworkTest]
    public class CatEnabledDoesStatusCodeRollupNaming : NewRelicIntegrationTest<BasicMvcApplicationTestFixture>
    {
        private BasicMvcApplicationTestFixture _fixture;
        private HttpResponseHeaders _responseHeaders;

        public CatEnabledDoesStatusCodeRollupNaming(BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    _fixture.RemoteApplication.NewRelicConfig.EnableCat();
                },
                exerciseApplication: () =>
                {
                    _fixture.GetIgnored();
                    _responseHeaders = _fixture.GetWithCatHeaderWithRedirectAndStatusCodeRollup();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        [Trait("feature", "CAT-DistributedTracing")]
        public void Test()
        {
            var catResponseHeader = _responseHeaders.GetValues(@"X-NewRelic-App-Data")?.FirstOrDefault();
            Assert.NotNull(catResponseHeader);
            var catResponseData = HeaderEncoder.DecodeAndDeserialize<CrossApplicationResponseData>(catResponseHeader, HeaderEncoder.IntegrationTestEncodingKey);
            var transactionEventRedirect = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/StatusCode/301");
            Assert.NotNull(transactionEventRedirect);
            Assert.Equal("WebTransaction/StatusCode/301", catResponseData.TransactionName);
        }
    }
}
