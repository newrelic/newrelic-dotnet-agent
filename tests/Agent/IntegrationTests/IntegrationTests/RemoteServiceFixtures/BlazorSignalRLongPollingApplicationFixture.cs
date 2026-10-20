// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

// The app restricts the Blazor circuit hub to the transport named in BLAZOR_SIGNALR_TRANSPORT.
public class BlazorSignalRLongPollingApplicationFixture : BlazorSignalRApplicationFixture
{
    public BlazorSignalRLongPollingApplicationFixture()
    {
        SetAdditionalEnvironmentVariable("BLAZOR_SIGNALR_TRANSPORT", "LongPolling");
    }
}
