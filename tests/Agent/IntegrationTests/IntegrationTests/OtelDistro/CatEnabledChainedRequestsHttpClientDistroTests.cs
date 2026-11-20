// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTests.CatOutbound;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.OtelDistro;

// The HttpClient CAT chain web test (CatOutbound.CatEnabledChainedRequestsHttpClient) in OTel distro mode: the
// BasicMvcApplication runs in hosted web core on .NET Framework (a HostedWebCore.exe copy named w3wp.exe, see
// RemoteWebApplication) under the OTel native profiler + the New Relic distro plugin, so NR's ASP.NET and HttpClient
// wrappers fire from inside OTel. Same assertions as the agent-mode class, except CallerTransactionTrace (skipped below:
// the GetIgnored() warm-up is not ignored, so it can take the one trace slot).
// This namespace is deliberately not in integration_all (.github/workflows/test_selection.yml): not run by CI until the
// distro is packaged. Requires NR_DEV_OTEL_HOME.
[Trait("Platform", "WindowsOnly")]
public class CatEnabledChainedRequestsHttpClientDistro : CatEnabledChainedRequestsHttpClientTestsBase<BasicMvcApplicationTestFixtureDistro>
{
    public CatEnabledChainedRequestsHttpClientDistro(BasicMvcApplicationTestFixtureDistro fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    [Fact(Skip = "Distro mode: Agent API (IgnoreTransaction) is a no-op until spike 03")]
    public override void CallerTransactionTrace() { }
}
