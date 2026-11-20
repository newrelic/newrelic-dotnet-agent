// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.DistributedTracing;

// Generic on the fixture so the same assertions run in agent mode and in OTel distro mode (hosted web core under the
// OTel native profiler + the New Relic distro plugin). The distro subclass lives in the OtelDistro namespace, which CI
// does not select (it needs NR_DEV_OTEL_HOME).
public abstract class InitiateDTAttributesTestBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : RemoteServiceFixtures.DTBasicMVCApplicationFixture
{

    private static readonly IReadOnlyList<string> ExpectedAttributes = new List<string>()
    {
        "guid",
        "traceId",
        "priority",
        "sampled"
    };

    private readonly TFixture _fixture;

    protected InitiateDTAttributesTestBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
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
            },
            exerciseApplication: () =>
            {
                _fixture.Initiate();
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var transactionEvent = _fixture.AgentLog.GetTransactionEvents().FirstOrDefault();

        var transactionSample = _fixture.AgentLog.GetTransactionSamples()
            .FirstOrDefault(sample => sample.Path == @"WebTransaction/MVC/DistributedTracingController/Initiate");

        NrAssert.Multiple(
            () => Assertions.TransactionEventHasAttributes(ExpectedAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
            () => Assertions.TransactionTraceHasAttributes(ExpectedAttributes, TransactionTraceAttributeType.Intrinsic, transactionSample)
        );
    }

    // Split out of Test() so the distro subclass can skip it: the Initiate action calls NoticeError (Agent API).
    [Fact]
    public virtual void ErrorAttributes()
    {
        var errorEvent = _fixture.AgentLog.GetErrorEvents().FirstOrDefault();
        var errorTrace = _fixture.AgentLog.GetErrorTraces().FirstOrDefault();

        NrAssert.Multiple(
            () => Assertions.ErrorEventHasAttributes(ExpectedAttributes, EventAttributeType.Intrinsic, errorEvent),
            () => Assertions.ErrorTraceHasAttributes(ExpectedAttributes, ErrorTraceAttributeType.Intrinsic, errorTrace)
        );
    }
}

[Trait("Platform", "WindowsOnly")]
public class InitiateDTAttributesTest : InitiateDTAttributesTestBase<RemoteServiceFixtures.DTBasicMVCApplicationFixture>
{
    public InitiateDTAttributesTest(RemoteServiceFixtures.DTBasicMVCApplicationFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
