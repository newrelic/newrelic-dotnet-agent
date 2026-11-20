// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTests.MultiAppDomain;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.OtelDistro;

// The multi-AppDomain web tests (MultiAppDomain.MultiAppDomainCachingEnabledTests) in OTel distro mode: two ASP.NET
// applications in one hosted web core process on .NET Framework (a HostedWebCore.exe copy named w3wp.exe, see
// RemoteWebApplication) under the OTel native profiler + the New Relic distro plugin. One arm only (the AppDomain-caching
// variable unset): AppDomain caching is a New Relic profiler mechanism, which distro mode does not attach. Same
// assertions as the agent-mode classes, except the two skipped below.
// This namespace is deliberately not in integration_all (.github/workflows/test_selection.yml): not run by CI until the
// distro is packaged. Requires NR_DEV_OTEL_HOME.
[Trait("Platform", "WindowsOnly")]
public class MultiAppDomainDistroTests : MultiAppDomainCachingTestsBase<MultiAppDomainWebApplicationFixtureDistro>
{
    public MultiAppDomainDistroTests(MultiAppDomainWebApplicationFixtureDistro fixture, ITestOutputHelper output)
        : base(fixture, output, appDomainCachingDisabled: false, expectedCallingStrategy: null)
    {
    }

    [Fact(Skip = "Distro mode: NR profiler calling strategy has no OTel equivalent")]
    public override void ProfilerUsesTheCallingStrategyThisArmSelected() { }

    [Fact(Skip = "Distro mode: Agent API (AddCustomAttribute) is a no-op until spike 03")]
    public override void EachAppReportsOnlyItsOwnCustomAttributes() { }
}
