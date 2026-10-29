// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.HSM;

public abstract class AspNetCoreLocalHSMEnabledAndServerSideHSMDisabledTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture
{
    private const string QueryStringParameterValue = @"my thing";


    private readonly TFixture _fixture;

    protected AspNetCoreLocalHSMEnabledAndServerSideHSMDisabledTestsBase(TFixture fixture, ITestOutputHelper output)
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
        // This test looks for the connect response body that was intended to be removed in P17, but was not.  If it does get removed this will fail.
        // 12/14/23 - the response status changed from "Gone" to "Conflict". If this test fails in the future, be alert for it possibly changing back.
        var notConnectedLogLine = _fixture.AgentLog.TryGetLogLine(AgentLogBase.ErrorResponseLogLinePrefixRegex + "Received HTTP status code Conflict *?");
        Assert.NotNull(notConnectedLogLine);
    }
}

public class AspNetCoreLocalHSMEnabledAndServerSideHSMDisabledTestsCoreOldest : AspNetCoreLocalHSMEnabledAndServerSideHSMDisabledTestsBase<RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixtureCoreOldest>
{
    public AspNetCoreLocalHSMEnabledAndServerSideHSMDisabledTestsCoreOldest(RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class AspNetCoreLocalHSMEnabledAndServerSideHSMDisabledTestsCoreLatest : AspNetCoreLocalHSMEnabledAndServerSideHSMDisabledTestsBase<RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixtureCoreLatest>
{
    public AspNetCoreLocalHSMEnabledAndServerSideHSMDisabledTestsCoreLatest(RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
