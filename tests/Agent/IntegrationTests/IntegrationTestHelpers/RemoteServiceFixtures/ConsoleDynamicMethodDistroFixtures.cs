// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

// OTel distro variants of the console MFA fixtures: the app runs under the OpenTelemetry .NET auto-instrumentation
// native profiler + the New Relic distro plugin instead of the New Relic profiler (RemoteApplication.UseOtelDistro).
// They subclass the agent-mode fixtures so tests that branch on the fixture type (e.g. Couchbase3TestsBase) pick
// the same runtime-specific expectations. Requires NR_DEV_OTEL_HOME.

public class ConsoleDynamicMethodFixtureCoreOldestDistro : ConsoleDynamicMethodFixtureCoreOldest
{
    public ConsoleDynamicMethodFixtureCoreOldestDistro()
    {
        RemoteApplication.UseOtelDistro = true;
    }
}

// Unused while Couchbase3DistroTestsCoreLatest is skipped (OTel v1.18.0 has no net11.0 runtime support).
public class ConsoleDynamicMethodFixtureCoreLatestDistro : ConsoleDynamicMethodFixtureCoreLatest
{
    public ConsoleDynamicMethodFixtureCoreLatestDistro()
    {
        RemoteApplication.UseOtelDistro = true;
    }
}

public class ConsoleDynamicMethodFixtureFW48Distro : ConsoleDynamicMethodFixtureFW48
{
    public ConsoleDynamicMethodFixtureFW48Distro()
    {
        RemoteApplication.UseOtelDistro = true;
    }
}

public class ConsoleDynamicMethodFixtureFW471Distro : ConsoleDynamicMethodFixtureFW471
{
    public ConsoleDynamicMethodFixtureFW471Distro()
    {
        RemoteApplication.UseOtelDistro = true;
    }
}

public class ConsoleDynamicMethodFixtureFWLatestDistro : ConsoleDynamicMethodFixtureFWLatest
{
    public ConsoleDynamicMethodFixtureFWLatestDistro()
    {
        RemoteApplication.UseOtelDistro = true;
    }
}
