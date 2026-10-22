// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.HSM;

public abstract class AspNetCoreLocalHSMEnabledAndServerSideHSMEnabledTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : RemoteServiceFixtures.HSMAspNetCoreMvcBasicRequestsFixture
{
    private const string QueryStringParameterValue = @"my thing";


    private readonly TFixture _fixture;

    protected AspNetCoreLocalHSMEnabledAndServerSideHSMEnabledTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
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
                configModifier.AddAttributesInclude("request.parameters.*");

                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "service" }, "ssl", "false");
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "recordSql", "raw");
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "highSecurity" }, "enabled", "true");
            },
            exerciseApplication: () =>
            {
                _fixture.GetWithData(QueryStringParameterValue);
                _fixture.ThrowException();
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var unexpectedAgentAttributes = new List<string>
        {
            @"request.parameters.data",
        };

        var expectedTransactionTraceAgentAttributes = new Dictionary<string, object>
        {
            { "response.status", "200" },
            { "http.statusCode", 200 }
        };
        var expectedTransactionEventIntrinsicAttributes1 = new Dictionary<string, string>
        {
            {"type", "Transaction"}
        };
        var expectedTransactionEventIntrinsicAttributes2 = new List<string>
        {
            "timestamp",
            "duration",
            "webDuration",
            "totalTime",
            "name"
        };
        var expectedTransactionEventAgentAttributes = new Dictionary<string, object>
        {
            { "response.status", "200" },
            { "http.statusCode", 200 }
        };

        var expectedErrorTransactionEventAttributes = new List<string>
        {
            "errorType",
            "errorMessage",
            "error"
        };


        var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/Home/Query/{data}");
        var getDataTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/Home/Query/{data}");
        var getExceptionTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/Home/ThrowException");

        NrAssert.Multiple(
            () => Assert.NotNull(transactionSample),
            () => Assert.NotNull(getDataTransactionEvent),
            () => Assert.NotNull(getExceptionTransactionEvent)
        );

        NrAssert.Multiple
        (
            () => Assertions.TransactionTraceDoesNotHaveAttributes(unexpectedAgentAttributes, TransactionTraceAttributeType.Agent, transactionSample),
            () => Assertions.TransactionTraceHasAttributes(expectedTransactionTraceAgentAttributes, TransactionTraceAttributeType.Agent, transactionSample),
            () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes1, TransactionEventAttributeType.Intrinsic, getDataTransactionEvent),
            () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes2, TransactionEventAttributeType.Intrinsic, getDataTransactionEvent),
            () => Assertions.TransactionEventHasAttributes(expectedTransactionEventAgentAttributes, TransactionEventAttributeType.Agent, getDataTransactionEvent),
            () => Assertions.TransactionEventHasAttributes(expectedErrorTransactionEventAttributes, TransactionEventAttributeType.Intrinsic, getExceptionTransactionEvent)
        );
    }
}

public class AspNetCoreLocalHSMEnabledAndServerSideHSMEnabledTestsCoreOldest : AspNetCoreLocalHSMEnabledAndServerSideHSMEnabledTestsBase<RemoteServiceFixtures.HSMAspNetCoreMvcBasicRequestsFixtureCoreOldest>
{
    public AspNetCoreLocalHSMEnabledAndServerSideHSMEnabledTestsCoreOldest(RemoteServiceFixtures.HSMAspNetCoreMvcBasicRequestsFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class AspNetCoreLocalHSMEnabledAndServerSideHSMEnabledTestsCoreLatest : AspNetCoreLocalHSMEnabledAndServerSideHSMEnabledTestsBase<RemoteServiceFixtures.HSMAspNetCoreMvcBasicRequestsFixtureCoreLatest>
{
    public AspNetCoreLocalHSMEnabledAndServerSideHSMEnabledTestsCoreLatest(RemoteServiceFixtures.HSMAspNetCoreMvcBasicRequestsFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
