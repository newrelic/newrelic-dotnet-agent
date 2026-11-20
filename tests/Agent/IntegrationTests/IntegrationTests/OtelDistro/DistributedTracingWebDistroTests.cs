// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTests.DistributedTracing;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.OtelDistro;

// The distributed-tracing web tests (DistributedTracing.*) in OTel distro mode: the BasicMvcApplication runs in hosted
// web core on .NET Framework (a HostedWebCore.exe copy named w3wp.exe, see RemoteWebApplication) under the OTel native
// profiler + the New Relic distro plugin. Same assertions as the agent-mode classes, except the error event / error
// trace facts (skipped below: DistributedTracingController calls NoticeError, an Agent API call).
// This namespace is deliberately not in integration_all (.github/workflows/test_selection.yml): not run by CI until the
// distro is packaged. Requires NR_DEV_OTEL_HOME.

[Trait("Platform", "WindowsOnly")]
public class DTSupportabilityMetricTestsDistro : DTSupportabilityMetricTestsBase<DTBasicMVCApplicationFixtureDistro>
{
    public DTSupportabilityMetricTestsDistro(DTBasicMVCApplicationFixtureDistro fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class InitiateDTAttributesTestDistro : InitiateDTAttributesTestBase<DTBasicMVCApplicationFixtureDistro>
{
    public InitiateDTAttributesTestDistro(DTBasicMVCApplicationFixtureDistro fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    [Fact(Skip = "Distro mode: Agent API (NoticeError) is a no-op until spike 03")]
    public override void ErrorAttributes() { }
}

[Trait("Platform", "WindowsOnly")]
public class ReceiveDTAttributesTestDistro : ReceiveDTAttributesTestBase<DTBasicMVCApplicationFixtureDistro>
{
    public ReceiveDTAttributesTestDistro(DTBasicMVCApplicationFixtureDistro fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    [Fact(Skip = "Distro mode: Agent API (NoticeError) is a no-op until spike 03")]
    public override void ErrorAttributes() { }
}
