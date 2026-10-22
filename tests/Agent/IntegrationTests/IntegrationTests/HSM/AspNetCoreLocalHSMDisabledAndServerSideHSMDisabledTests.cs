// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.HSM;

public abstract class AspNetCoreLocalHSMDisabledAndServerSideHSMDisabledTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture
{
    private const string QueryStringParameterValue = @"my thing";


    private readonly TFixture _fixture;

    protected AspNetCoreLocalHSMDisabledAndServerSideHSMDisabledTestsBase(TFixture fixture, ITestOutputHelper output)
        : base(fixture)
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
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "highSecurity" }, "enabled", "false");
            },
            exerciseApplication: () => _fixture.GetWithData(QueryStringParameterValue)
        );
        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var expectedAttributes = new Dictionary<string, string>
        {
            { "request.parameters.data", QueryStringParameterValue },
        };

        var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
        Assertions.TransactionTraceHasAttributes(expectedAttributes, TransactionTraceAttributeType.Agent, transactionSample);
    }
}

public class AspNetCoreLocalHSMDisabledAndServerSideHSMDisabledTestsCoreOldest : AspNetCoreLocalHSMDisabledAndServerSideHSMDisabledTestsBase<RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixtureCoreOldest>
{
    public AspNetCoreLocalHSMDisabledAndServerSideHSMDisabledTestsCoreOldest(RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class AspNetCoreLocalHSMDisabledAndServerSideHSMDisabledTestsCoreLatest : AspNetCoreLocalHSMDisabledAndServerSideHSMDisabledTestsBase<RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixtureCoreLatest>
{
    public AspNetCoreLocalHSMDisabledAndServerSideHSMDisabledTestsCoreLatest(RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
