// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

// The app restricts the Blazor circuit hub to the transport named in BLAZOR_SIGNALR_TRANSPORT.
public abstract class BlazorSignalRLongPollingApplicationFixture : BlazorSignalRApplicationFixture
{
    protected BlazorSignalRLongPollingApplicationFixture(string targetFramework) : base(targetFramework)
    {
        SetAdditionalEnvironmentVariable("BLAZOR_SIGNALR_TRANSPORT", "LongPolling");
    }
}

public class BlazorSignalRLongPollingApplicationFixtureCoreOldest : BlazorSignalRLongPollingApplicationFixture
{
    public BlazorSignalRLongPollingApplicationFixtureCoreOldest() : base(Tfm.NetOldest)
    {
    }
}

public class BlazorSignalRLongPollingApplicationFixtureCoreLatest : BlazorSignalRLongPollingApplicationFixture
{
    public BlazorSignalRLongPollingApplicationFixtureCoreLatest() : base(Tfm.NetLatest)
    {
    }
}
