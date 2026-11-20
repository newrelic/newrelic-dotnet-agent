// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.UnboundedIntegrationTests.Couchbase;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.OtelDistro;

// Couchbase3 tests with the app running under the OTel distro (OTel native profiler + New Relic distro plugin)
// instead of the New Relic profiler. Same commands and assertions as the agent-mode Couchbase3 tests.
// This namespace is deliberately not in unbounded_all (.github/workflows/test_selection.yml): not run by CI
// until the distro is packaged. Requires NR_DEV_OTEL_HOME.

public class Couchbase3DistroTestsCoreOldest : Couchbase3TestsBase<ConsoleDynamicMethodFixtureCoreOldestDistro>
{
    public Couchbase3DistroTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldestDistro fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

// Skipped: Couchbase3TestsBase's single [Fact] is non-virtual and starts the app in the base constructor, so a
// skipped subclass of it is not possible. This stand-in keeps the class name and reports the skip reason.
// When an OTel home with net11.0 support is available, restore it as
// "Couchbase3TestsBase<ConsoleDynamicMethodFixtureCoreLatestDistro>" like the other three classes.
public class Couchbase3DistroTestsCoreLatest
{
    [Fact(Skip = "OTel .NET auto-instrumentation v1.18.0 has no net11.0 runtime support; the distro .NET proof uses Couchbase3DistroTestsCoreOldest (net10.0).")]
    public void Test()
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class Couchbase3DistroTestsFW48 : Couchbase3TestsBase<ConsoleDynamicMethodFixtureFW48Distro>
{
    public Couchbase3DistroTestsFW48(ConsoleDynamicMethodFixtureFW48Distro fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class Couchbase3DistroTestsFW471 : Couchbase3TestsBase<ConsoleDynamicMethodFixtureFW471Distro>
{
    public Couchbase3DistroTestsFW471(ConsoleDynamicMethodFixtureFW471Distro fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
