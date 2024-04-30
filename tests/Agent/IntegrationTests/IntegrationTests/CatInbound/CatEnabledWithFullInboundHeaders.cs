// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CatInbound
{
    [NetFrameworkTest]
    public class CatEnabledWithFullInboundHeaders : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {

        private RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;


        private HttpResponseHeaders _responseHeaders;

        public CatEnabledWithFullInboundHeaders(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture)
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
                    configModifier.EnableCat();
                },
                exerciseApplication: () =>
                {
                    _fixture.GetIgnored();
                    _responseHeaders = _fixture.GetWithCatHeader(requestData: new CrossApplicationRequestData("guid", false, "tripId", "pathHash"));
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

            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/DefaultController/Index");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/Index");
            var metrics = _fixture.AgentLog.GetMetrics();

            NrAssert.Multiple
            (
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
            );

            var expectedTransactionTraceIntrinsicAttributes1 = new List<string>
            {
                "client_cross_process_id",
                "path_hash"
            };
            var expectedTransactionTraceIntrinsicAttributes2 = new Dictionary<string, string>
            {
				// These values come from what we send to the application (see parameter passed to GetWithCatHeader above)
				{"referring_transaction_guid", "guid"},
                {"trip_id", "tripId"}
            };
            var expectedTransactionEventIntrinsicAttributes1 = new List<string>
            {
                "nr.guid",
                "nr.pathHash"
            };
            var expectedTransactionEventIntrinsicAttributes2 = new Dictionary<string, string>
            {
				// These values come from what we send to the application (see parameter passed to GetWithCatHeader above)
				{"nr.referringPathHash", "pathHash"},
                {"nr.referringTransactionGuid", "guid"},
                {"nr.tripId", "tripId"}
            };

            NrAssert.Multiple
            (
                () => Assert.Equal(_fixture.AgentLog.GetCrossProcessId(), catResponseData.CrossProcessId),
                () => Assert.Equal("WebTransaction/MVC/DefaultController/Index", catResponseData.TransactionName),
                () => Assert.True(catResponseData.QueueTimeInSeconds >= 0),
                () => Assert.True(catResponseData.ResponseTimeInSeconds >= 0),
                () => Assert.Equal(-1, catResponseData.ContentLength),
                () => Assert.NotNull(catResponseData.TransactionGuid),
                () => Assert.False(catResponseData.Unused),

                // Trace attributes
                () => Assertions.TransactionTraceHasAttributes(expectedTransactionTraceIntrinsicAttributes1, TransactionTraceAttributeType.Intrinsic, transactionSample),
                () => Assertions.TransactionTraceHasAttributes(expectedTransactionTraceIntrinsicAttributes2, TransactionTraceAttributeType.Intrinsic, transactionSample),

                // Event attributes
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes1, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes2, TransactionEventAttributeType.Intrinsic, transactionEvent),

                () => Assertions.MetricsExist(Expectations.ExpectedMetricsCatEnabled, metrics)
            );
        }
    }
}
